using System.Reflection;
using System.Runtime.CompilerServices;
using Admin.Modules;
using Catalog.Modules;
using Identity.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModularMonolith.Api;
using SharedKernel.Events;
using Orders.Modules;
using Reporting.Modules;

namespace ModularMonolith.Architecture.Tests;

public class GuardrailTests(HostWithoutDatabaseFactory factory) : IClassFixture<HostWithoutDatabaseFactory>
{
    public static TheoryData<Type> ModuleTypes() =>
    [
        typeof(CatalogModule),
        typeof(OrdersModule),
        typeof(AdministrationModule),
        typeof(IdentityModule),
        typeof(ReportingModule)
    ];

    [Theory]
    [MemberData(nameof(ModuleTypes))]
    public void Module_Has_At_Most_One_InternalsVisibleTo_And_It_Is_Its_Allowed_Test_Project(Type moduleType)
    {
        var moduleAssembly = moduleType.Assembly;
        var moduleName = moduleAssembly.GetName().Name!;
        Assert.True(
            ArchitectureConstants.AllowedInternalsVisibleTo.TryGetValue(moduleName, out var allowed),
            $"{moduleName} has no entry in ArchitectureConstants.AllowedInternalsVisibleTo.");

        var granted = moduleAssembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .ToArray();

        if (allowed is null)
        {
            Assert.Empty(granted);
        }
        else
        {
            Assert.True(granted.Length <= 1, $"{moduleName} grants InternalsVisibleTo to: {string.Join(", ", granted)}.");
            Assert.All(granted, name => Assert.Equal(allowed, name));
        }
    }

    [Fact]
    public void No_Module_Service_Resolves_To_Another_Modules_Type()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        var modules = HostComposition.ConfigureServices(builder);
        var moduleAssemblies = modules.Select(module => module.GetType().Assembly).ToHashSet();
        var firstPartyAssemblies = moduleAssemblies.Append(typeof(HostComposition).Assembly).ToHashSet();

        using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var checkedDependencies = 0;
        var violations = new List<string>();
        foreach (var moduleAssembly in moduleAssemblies)
        {
            var serviceClasses = builder.Services
                .Select(descriptor => descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType)
                .OfType<Type>()
                .Where(type => type.Assembly == moduleAssembly && !type.IsVisible && !type.ContainsGenericParameters)
                .Distinct();

            foreach (var serviceClass in serviceClasses)
            {
                foreach (var parameter in serviceClass.GetConstructors().SelectMany(ctor => ctor.GetParameters()))
                {
                    var resolved = scope.ServiceProvider.GetService(parameter.ParameterType);
                    if (resolved is null)
                    {
                        continue;
                    }

                    checkedDependencies++;
                    var implementationAssembly = resolved.GetType().Assembly;
                    if (!IsAllowedDependency(implementationAssembly, moduleAssembly, firstPartyAssemblies))
                    {
                        violations.Add(
                            $"{serviceClass.FullName}({parameter.ParameterType.Name} {parameter.Name}) resolves to " +
                            $"{resolved.GetType().FullName} in {implementationAssembly.GetName().Name}.");
                    }
                }
            }
        }

        Assert.True(checkedDependencies > 0, "No module service constructor dependencies were found; the test is not checking anything.");
        Assert.Empty(violations);
    }

    // Allowed: the module itself, the shared kernel (SharedKernel.* until phase 2 removes the
    // persistence assemblies), any *.Contracts assembly, and anything outside the solution.
    private static bool IsAllowedDependency(Assembly implementation, Assembly consumer, HashSet<Assembly> firstParty)
    {
        var name = implementation.GetName().Name ?? string.Empty;
        return implementation == consumer
               || name.StartsWith(ArchitectureConstants.SharedKernelAssembly, StringComparison.Ordinal)
               || name.EndsWith(".Contracts", StringComparison.Ordinal)
               || !firstParty.Contains(implementation);
    }

    [Fact]
    public void Integration_Event_Handlers_Resolve_Only_Within_Their_Own_Module_Key()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        HostComposition.ConfigureServices(builder);
        using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var subscriptions = scope.ServiceProvider.GetServices<IntegrationEventSubscription>().ToArray();
        Assert.NotEmpty(subscriptions);

        foreach (var subscription in subscriptions)
        {
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(subscription.EventType);

            // Never unkeyed: an unkeyed resolve would hand every module's handlers to anyone who asks.
            Assert.Empty(scope.ServiceProvider.GetServices(handlerType));

            // Under a module's key, only that module's handlers: the assembly of the context with the same key.
            var moduleAssembly = scope.ServiceProvider.GetRequiredKeyedService<DbContext>(subscription.ModuleKey).GetType().Assembly;
            var handlers = scope.ServiceProvider.GetKeyedServices(handlerType, subscription.ModuleKey).ToArray();
            Assert.NotEmpty(handlers);
            Assert.All(handlers, handler => Assert.Equal(moduleAssembly, handler!.GetType().Assembly));
        }
    }

    [Fact]
    public void Every_Authorization_Policy_Referenced_By_An_Endpoint_Is_Registered()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var policyProvider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.Contains(endpoints, endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => data.Policy is not null));
        Assert.Empty(ModuleComposition.FindUnknownPolicies(endpoints, policyProvider));
    }

    [Fact]
    public void Every_Endpoint_Policy_Name_Is_An_Identity_Contracts_Constant()
    {
        // The runtime form of "no string literal policy names": whatever the source says, every policy an
        // endpoint requires must be one of the compiled names Identity publishes (ADR-0011).
        var compiledNames = new[] { typeof(Identity.Contracts.Permissions), typeof(Identity.Contracts.Policies) }
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        var used = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>())
            .Select(data => data.Policy)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(used);
        Assert.Empty(used.Except(compiledNames));
    }

    [Fact]
    public void Every_Module_Endpoint_Has_Exactly_Its_Modules_Rate_Limit_Policy()
    {
        // Applied once, on the module's route group (ADR-0013). A per-endpoint RequireRateLimiting would show
        // up here as a second policy or a different one.
        var expected = new Dictionary<string, string>
        {
            ["api/catalog"] = SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.Catalog,
            ["api/orders"] = SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.Orders,
            ["api/admin"] = SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.Administration,
            ["api/identity"] = SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.Identity,
            ["api/reporting"] = SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.Reporting
        };

        var moduleEndpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (endpoint, prefix: expected.Keys.SingleOrDefault(prefix =>
                (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/').StartsWith(prefix, StringComparison.Ordinal))))
            .Where(entry => entry.prefix is not null)
            .ToArray();
        Assert.NotEmpty(moduleEndpoints);

        var violations = moduleEndpoints
            .Select(entry => (entry.endpoint, entry.prefix, policies: entry.endpoint.Metadata
                .GetOrderedMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()
                .Select(attribute => attribute.PolicyName).ToArray()))
            .Where(entry => entry.policies.Length != 1 || entry.policies[0] != expected[entry.prefix!])
            .Select(entry => $"{entry.endpoint.DisplayName}: [{string.Join(", ", entry.policies)}]")
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void No_Two_Endpoints_Share_Route_And_Method()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        Assert.NotEmpty(endpoints);
        Assert.Empty(ModuleComposition.FindDuplicateRoutes(endpoints));
    }

    [Fact]
    public void Endpoint_Validation_Reports_Duplicate_Routes_And_Unknown_Policies()
    {
        Endpoint[] endpoints =
        [
            FakeEndpoint("/api/catalog/albums", "GET", "first"),
            FakeEndpoint("/API/Catalog/Albums/", "GET", "second"),
            FakeEndpoint("/api/catalog/albums", "POST", "third", "music.write")
        ];
        var policyProvider = new DefaultAuthorizationPolicyProvider(Options.Create(new AuthorizationOptions()));

        var duplicate = Assert.Single(ModuleComposition.FindDuplicateRoutes(endpoints));
        Assert.Contains("first", duplicate, StringComparison.Ordinal);
        Assert.Contains("second", duplicate, StringComparison.Ordinal);

        var unknown = Assert.Single(ModuleComposition.FindUnknownPolicies(endpoints, policyProvider));
        Assert.Contains("'music.write'", unknown, StringComparison.Ordinal);
    }

    private static RouteEndpoint FakeEndpoint(string route, string method, string name, string? policy = null)
    {
        var metadata = new List<object> { new HttpMethodMetadata([method]) };
        if (policy is not null)
        {
            metadata.Add(new AuthorizeAttribute(policy));
        }

        return new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(route),
            order: 0,
            new EndpointMetadataCollection(metadata),
            name);
    }
}

/// <summary>
/// The real host with startup migration, seeding and the outbox dispatcher switched off. These tests read the composed
/// endpoints and services only, so they need no database.
/// </summary>
public sealed class HostWithoutDatabaseFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:MigrateAndSeedOnStartup"] = "false",
                ["Outbox:Enabled"] = "false"
            }));
    }
}
