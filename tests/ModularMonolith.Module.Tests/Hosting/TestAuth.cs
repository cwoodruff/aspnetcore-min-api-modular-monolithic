using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Identity.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModularMonolith.Module.Tests.Hosting;

/// <summary>
///     Stands in for the Identity module in a single-module host: a test authentication scheme that reads the
///     caller from request headers, and the authorization policies Identity.Contracts names, with the same
///     rules Identity registers. The module under test sees the contract it compiles against, not Identity.
/// </summary>
public static class TestAuth
{
    public const string Scheme = "Test";
    public const string UserHeader = "X-Test-User";
    public const string PermissionsHeader = "X-Test-Permissions";
    public const string RolesHeader = "X-Test-Roles";
    public const string TenantHeader = "X-Test-Tenant";
    public const string RequestTenantHeader = "X-Tenant-Id";

    public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(Scheme).AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(Scheme, null);
        services.AddHttpContextAccessor();
        services.AddAuthorization(options =>
        {
            foreach (var permission in Constants(typeof(Permissions)))
            {
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("permissions", permission));
            }

            options.AddPolicy(Policies.Admin, policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));

            // Identity's tenant rule: a tenant claim is required; with an X-Tenant-Id header it must match.
            options.AddPolicy(Policies.TenantScoped, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
            {
                var userTenant = context.User.FindFirstValue("tenant");
                if (string.IsNullOrWhiteSpace(userTenant))
                {
                    return false;
                }

                var http = context.Resource as Microsoft.AspNetCore.Http.HttpContext;
                var requestTenant = http?.Request.Headers[RequestTenantHeader].ToString();
                return string.IsNullOrWhiteSpace(requestTenant) || string.Equals(requestTenant, userTenant, StringComparison.Ordinal);
            }));
        });
        return services;
    }

    private static IEnumerable<string> Constants(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);

    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var user) || string.IsNullOrWhiteSpace(user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new("sub", user.ToString()), new(ClaimTypes.Name, user.ToString()) };
            claims.AddRange(Split(PermissionsHeader).Select(permission => new Claim("permissions", permission)));
            claims.AddRange(Split(RolesHeader).Select(role => new Claim(ClaimTypes.Role, role)));
            if (Request.Headers.TryGetValue(TenantHeader, out var tenant) && !string.IsNullOrWhiteSpace(tenant))
            {
                claims.Add(new Claim("tenant", tenant.ToString()));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, TestAuth.Scheme));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, TestAuth.Scheme)));
        }

        private string[] Split(string header) =>
            Request.Headers.TryGetValue(header, out var value)
                ? value.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [];
    }
}

/// <summary>A caller for a module host: who they are, what they may do, which tenant they belong to.</summary>
public sealed record TestUser(string[] Permissions, string[] Roles, string Tenant = "tenant-123", string Name = "user-1")
{
    /// <summary>The tenant user the full-host tests used by default: catalog.read and orders.read.</summary>
    public static TestUser TenantUser(string tenant = "tenant-123", string[]? permissions = null, string[]? roles = null) =>
        new(permissions ?? [Identity.Contracts.Permissions.CatalogRead, Identity.Contracts.Permissions.OrdersRead],
            roles ?? ["User"], tenant);

    /// <summary>An administrator with the administration permissions.</summary>
    public static TestUser Admin(string tenant = "tenant-123", string[]? permissions = null) =>
        new(permissions ??
            [
                Identity.Contracts.Permissions.CatalogRead, Identity.Contracts.Permissions.OrdersRead,
                Identity.Contracts.Permissions.AdminUsersManage, Identity.Contracts.Permissions.AdministrationRead,
                Identity.Contracts.Permissions.AdministrationWrite
            ],
            ["Admin"], tenant);
}
