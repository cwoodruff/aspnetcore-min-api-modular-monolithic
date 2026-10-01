using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace ModularMonolith.Api;

/// <summary>
/// Startup checks over the composed endpoint list. The architecture tests call the same
/// Find* methods, so a mistake fails both the test run and a Development/Test boot.
/// </summary>
public static class ModuleComposition
{
    private const string AnyMethod = "*";

    /// <summary>
    /// Throws if two endpoints share a route and HTTP method, or if an endpoint references an
    /// authorization policy that is not registered. Call after every module has mapped its endpoints.
    /// </summary>
    public static void ValidateEndpoints(WebApplication app)
    {
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToArray();
        var policyProvider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var problems = FindDuplicateRoutes(endpoints)
            .Concat(FindUnknownPolicies(endpoints, policyProvider))
            .ToArray();

        if (problems.Length > 0)
        {
            throw new InvalidOperationException(
                "Endpoint validation failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(problem => "  - " + problem)));
        }
    }

    /// <summary>
    /// Returns one message per (route, HTTP method) pair mapped by more than one endpoint.
    /// Routes compare case-insensitively, as routing does. An endpoint with no method metadata
    /// matches every method and is reported as "*".
    /// </summary>
    public static IReadOnlyList<string> FindDuplicateRoutes(IEnumerable<Endpoint> endpoints)
    {
        return endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint =>
            {
                var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
                return (methods is { Count: > 0 } ? methods : [AnyMethod])
                    .Select(method => (Route: Normalize(endpoint.RoutePattern.RawText), Method: method, endpoint.DisplayName));
            })
            .GroupBy(entry => (entry.Route, entry.Method), RouteKeyComparer.Instance)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{group.Key.Method} {group.Key.Route} is mapped by {group.Count()} endpoints: " +
                string.Join(", ", group.Select(entry => entry.DisplayName)))
            .ToArray();
    }

    /// <summary>
    /// Returns one message per policy name that an endpoint references but the policy provider
    /// cannot resolve.
    /// </summary>
    public static IReadOnlyList<string> FindUnknownPolicies(
        IEnumerable<Endpoint> endpoints,
        IAuthorizationPolicyProvider policyProvider)
    {
        return endpoints
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .OfType<string>()
                .Where(policy => policy.Length > 0)
                .Select(policy => (Policy: policy, endpoint.DisplayName)))
            .GroupBy(entry => entry.Policy, StringComparer.Ordinal)
            // Startup runs without a synchronization context; the default provider completes synchronously.
            .Where(group => policyProvider.GetPolicyAsync(group.Key).GetAwaiter().GetResult() is null)
            .Select(group =>
                $"Policy '{group.Key}' is not registered but is required by: " +
                string.Join(", ", group.Select(entry => entry.DisplayName).Distinct()))
            .ToArray();
    }

    private static string Normalize(string? rawText) => "/" + (rawText ?? string.Empty).Trim('/');

    private sealed class RouteKeyComparer : IEqualityComparer<(string Route, string Method)>
    {
        public static readonly RouteKeyComparer Instance = new();

        public bool Equals((string Route, string Method) x, (string Route, string Method) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Route, y.Route) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.Method, y.Method);

        public int GetHashCode((string Route, string Method) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Route),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Method));
    }
}
