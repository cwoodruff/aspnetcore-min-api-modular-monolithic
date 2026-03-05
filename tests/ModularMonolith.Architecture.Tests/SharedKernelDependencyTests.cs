using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModularMonolith.Architecture.Tests;

public class SharedKernelDependencyTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture =
        ArchitectureConstants.MainArchitecture;

    [Theory]
    [InlineData(ArchitectureConstants.SharedKernelAssembly)]
    [InlineData(ArchitectureConstants.SharedKernelPersistenceAssembly)]
    [InlineData(ArchitectureConstants.SharedKernelDataSQLiteAssembly)]
    public void SharedKernel_Should_Not_Depend_On_Any_Module(string sharedAssembly)
    {
        foreach (var moduleAssembly in ArchitectureConstants.AllModuleAssemblies)
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
