using System.Reflection;
using System.Runtime.CompilerServices;
using Admin.Modules;
using Catalog.Modules;
using Identity.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModularMonolith.Api;
using Orders.Modules;
using Reporting.Modules;

namespace ModularMonolith.Architecture.Tests;

public class GuardrailTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
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
                .Select(descriptor => descriptor.IsKeyedService ? null : descriptor.ImplementationType)
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
    public void Every_Authorization_Policy_Referenced_By_An_Endpoint_Is_Registered()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var policyProvider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.Contains(endpoints, endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => data.Policy is not null));
        Assert.Empty(ModuleComposition.FindUnknownPolicies(endpoints, policyProvider));
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
