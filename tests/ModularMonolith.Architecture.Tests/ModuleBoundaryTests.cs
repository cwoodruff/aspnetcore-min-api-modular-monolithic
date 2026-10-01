using ArchUnitNET.xUnit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularMonolith.Api;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModularMonolith.Architecture.Tests;

public class ModuleBoundaryTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture =
        ArchitectureConstants.MainArchitecture;

    public static TheoryData<string, string> ModulePairs()
    {
        var data = new TheoryData<string, string>();
        var modules = ArchitectureConstants.AllModuleAssemblies;
        for (var i = 0; i < modules.Length; i++)
        {
            for (var j = 0; j < modules.Length; j++)
            {
                if (i != j)
                    data.Add(modules[i], modules[j]);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Module_Should_Not_Depend_On_Other_Module(
        string sourceModule, string targetModule)
    {
        var rule = Types()
            .That()
            .ResideInAssembly(ArchitectureConstants.FullName(sourceModule))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInAssembly(ArchitectureConstants.FullName(targetModule))
            .Because($"{sourceModule} must not reference {targetModule} to maintain module isolation");

        rule.Check(Architecture);
    }

    [Theory]
    [InlineData(ArchitectureConstants.CatalogAssembly)]
    [InlineData(ArchitectureConstants.OrdersAssembly)]
    [InlineData(ArchitectureConstants.AdminAssembly)]
    [InlineData(ArchitectureConstants.IdentityAssembly)]
    [InlineData(ArchitectureConstants.ReportingAssembly)]
    public void Module_Should_Only_Depend_On_SharedKernel_And_Framework(string moduleName)
    {
        var otherModules = ArchitectureConstants.AllModuleAssemblies
            .Where(m => m != moduleName)
            .ToArray();

        foreach (var otherModule in otherModules)
        {
            var rule = Types()
                .That()
                .ResideInAssembly(ArchitectureConstants.FullName(moduleName))
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInAssembly(ArchitectureConstants.FullName(otherModule))
                .Because($"{moduleName} should only depend on SharedKernel and framework assemblies, not {otherModule}");

            rule.Check(Architecture);
        }
    }

    public static TheoryData<string, string> ContractsModulePairs()
    {
        var data = new TheoryData<string, string>();
        foreach (var contracts in ArchitectureConstants.AllContractsAssemblies)
        {
            foreach (var module in ArchitectureConstants.AllModuleAssemblies)
            {
                data.Add(contracts, module);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ContractsModulePairs))]
    public void Contracts_Should_Not_Depend_On_Any_Module(string contractsAssembly, string moduleAssembly)
    {
        // Contracts are what other modules compile against; a reference back into any module would
        // drag that module's internals into every consumer.
        var rule = Types()
            .That()
            .ResideInAssembly(ArchitectureConstants.FullName(contractsAssembly))
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInAssembly(ArchitectureConstants.FullName(moduleAssembly))
            .Because($"{contractsAssembly} is a public contract and must not reference {moduleAssembly}")
            // Catalog.Contracts and Administration.Contracts hold no types yet.
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    public void Each_Module_DbContext_Maps_Only_Its_Own_Entities()
    {
        // Build the container the app runs with; each module registers its context as a DbContext keyed by module.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        HostComposition.ConfigureServices(builder);
        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var contexts = scope.ServiceProvider.GetKeyedServices<DbContext>(KeyedService.AnyKey).ToArray();
        Assert.Equal(4, contexts.Length);

        // The one exception is the outbox/inbox plumbing SharedKernel defines for every module to map
        // into its own schema (ADR-0008); nothing domain-shaped lives in SharedKernel.
        var sharedKernel = typeof(SharedKernel.IModule).Assembly;
        var violations = contexts
            .SelectMany(context => context.Model.GetEntityTypes()
                .Where(entity => entity.ClrType.Assembly != context.GetType().Assembly
                                 && entity.ClrType.Assembly != sharedKernel)
                .Select(entity =>
                    $"{context.GetType().Name} maps {entity.ClrType.FullName} from {entity.ClrType.Assembly.GetName().Name}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void InvoiceFinalized_Handlers_Live_In_Catalog_And_Administration_And_Use_Only_Orders_Contracts()
    {
        var handlerInterface = typeof(SharedKernel.Events.IIntegrationEventHandler<Orders.Contracts.Events.InvoiceFinalized>);
        var handlers = ArchitectureConstants.AllModuleAssemblies
            .Select(System.Reflection.Assembly.Load)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && handlerInterface.IsAssignableFrom(type))
            .ToArray();

        Assert.Equal(
            [ArchitectureConstants.AdminAssembly, ArchitectureConstants.CatalogAssembly],
            handlers.Select(type => type.Assembly.GetName().Name!).Order(StringComparer.Ordinal));

        // They see the event through Orders.Contracts; Module_Should_Not_Depend_On_Other_Module covers the
        // whole assembly, this states it for the handlers themselves.
        foreach (var handler in handlers)
        {
            var rule = Types()
                .That()
                .HaveFullName(handler.FullName!)
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInAssembly(ArchitectureConstants.FullName(ArchitectureConstants.OrdersAssembly))
                .Because($"{handler.FullName} may know Orders only through Orders.Contracts");
            rule.Check(Architecture);
        }
    }
}
