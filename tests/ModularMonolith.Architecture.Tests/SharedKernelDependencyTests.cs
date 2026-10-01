using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModularMonolith.Architecture.Tests;

public class SharedKernelDependencyTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture =
        ArchitectureConstants.MainArchitecture;

    [Theory]
    [InlineData(ArchitectureConstants.SharedKernelAssembly)]
    public void SharedKernel_Should_Not_Depend_On_Any_Module_Or_Contracts(string sharedAssembly)
    {
        foreach (var moduleAssembly in ArchitectureConstants.AllModuleAssemblies
                     .Concat(ArchitectureConstants.AllContractsAssemblies))
        {
            var rule = Types()
                .That()
                .ResideInAssembly(sharedAssembly)
                .Should()
                .NotDependOnAnyTypesThat()
                .ResideInAssembly(moduleAssembly)
                .Because(
                    $"{sharedAssembly} is a shared kernel and must not depend on {moduleAssembly}")
                .WithoutRequiringPositiveResults();

            rule.Check(Architecture);
        }
    }
}
