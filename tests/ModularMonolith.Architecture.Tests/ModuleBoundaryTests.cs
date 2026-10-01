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
            .ResideInAssembly(sourceModule)
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInAssembly(targetModule)
            .Because($"{sourceModule} must not reference {targetModule} to maintain module isolation")
            .WithoutRequiringPositiveResults();

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
                .ResideInAssembly(moduleName)
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInAssembly(otherModule)
                .Because($"{moduleName} should only depend on SharedKernel and framework assemblies, not {otherModule}")
                .WithoutRequiringPositiveResults();

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
            .ResideInAssembly(contractsAssembly)
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInAssembly(moduleAssembly)
            .Because($"{contractsAssembly} is a public contract and must not reference {moduleAssembly}")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    public void Each_Module_DbContext_Maps_Only_Its_Own_Entities()
    {
        // Build the container the app runs with; each module registers its context as DbContext too.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        HostComposition.ConfigureServices(builder);
        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var contexts = scope.ServiceProvider.GetServices<DbContext>().ToArray();
        Assert.Equal(3, contexts.Length);

        var violations = contexts
            .SelectMany(context => context.Model.GetEntityTypes()
                .Where(entity => entity.ClrType.Assembly != context.GetType().Assembly)
                .Select(entity =>
                    $"{context.GetType().Name} maps {entity.ClrType.FullName} from {entity.ClrType.Assembly.GetName().Name}"))
            .ToArray();

        Assert.Empty(violations);
    }
}
