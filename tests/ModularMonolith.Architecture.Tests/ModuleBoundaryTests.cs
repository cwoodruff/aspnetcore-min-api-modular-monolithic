using ArchUnitNET.xUnit;
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
    [InlineData(ArchitectureConstants.MusicAssembly)]
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
}
