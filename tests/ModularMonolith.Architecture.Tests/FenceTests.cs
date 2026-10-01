using System.Reflection;
using System.Runtime.CompilerServices;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModularMonolith.Architecture.Tests;

/// <summary>
/// Phase 4 fences: what a Contracts project may contain, and how big and how dependent SharedKernel may
/// get (ADR-0011, ADR-0012).
/// </summary>
public class FenceTests
{
    /// <summary>
    /// SharedKernel's public types after phase 3 (28) plus two. Raising it is a deliberate change: edit this
    /// number, say why in the PR, and the reviewer sees it (ADR-0012).
    /// </summary>
    private const int SharedKernelPublicTypeBudget = 30;

    // Members the compiler generates for a record; anything else with a body is not a contract.
    private static readonly HashSet<string> RecordMembers =
    [
        "<Clone>$", "Deconstruct", "Equals", "GetHashCode", "ToString", "PrintMembers", "op_Equality", "op_Inequality"
    ];

    private static readonly ArchUnitNET.Domain.Architecture Architecture = ArchitectureConstants.MainArchitecture;

    public static TheoryData<string> ContractsAssemblies() => [.. ArchitectureConstants.AllContractsAssemblies];

    [Theory]
    [MemberData(nameof(ContractsAssemblies))]
    public void Contracts_Contain_Only_Interfaces_Enums_Records_And_Constant_Holders(string contractsAssembly)
    {
        var violations = Assembly.Load(contractsAssembly).GetTypes()
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .Select(type => (type, problem: ContractShapeProblem(type)))
            .Where(result => result.problem is not null)
            .Select(result => $"{result.type.FullName}: {result.problem}")
            .ToArray();

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(ContractsAssemblies))]
    public void Contracts_Grant_No_InternalsVisibleTo(string contractsAssembly)
    {
        Assert.Empty(Assembly.Load(contractsAssembly).GetCustomAttributes<InternalsVisibleToAttribute>());
    }

    [Fact]
    public void SharedKernel_Public_Surface_Stays_Within_Budget()
    {
        var exported = typeof(SharedKernel.IModule).Assembly.GetExportedTypes();

        Assert.True(exported.Length <= SharedKernelPublicTypeBudget,
            $"SharedKernel exports {exported.Length} public types; the budget is {SharedKernelPublicTypeBudget}. " +
            "Shrink it, or raise the budget in this test and say why in the PR (ADR-0012).");
    }

    [Fact]
    public void SharedKernel_References_No_FluentValidation_Module_Or_Contracts_Assembly()
    {
        var forbidden = typeof(SharedKernel.IModule).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("FluentValidation", StringComparison.Ordinal)
                           || ArchitectureConstants.AllModuleAssemblies.Contains(name)
                           || ArchitectureConstants.AllContractsAssemblies.Contains(name))
            .ToArray();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void SharedKernel_Uses_Npgsql_Only_From_Its_Persistence_Namespace()
    {
        // The one provider dependency the plan's phase 2 put in the kernel (ModuleDbContextOptions) stays in
        // one namespace; nothing else in SharedKernel may touch a PostgreSQL-specific type (ADR-0012).
        var rule = Types()
            .That()
            .ResideInAssembly(ArchitectureConstants.FullName(ArchitectureConstants.SharedKernelAssembly))
            .And()
            .DoNotResideInNamespace("SharedKernel.Persistence")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching("^Npgsql")
            .OrShould()
            .NotDependOnAnyTypesThat()
            .HaveFullNameMatching("^Microsoft\\.EntityFrameworkCore\\.Npgsql")
            .Because("only SharedKernel.Persistence may depend on Npgsql");

        rule.Check(Architecture);
    }

    private static string? ContractShapeProblem(Type type)
    {
        if (type.IsInterface || type.IsEnum)
        {
            return null;
        }

        if (type is { IsAbstract: true, IsSealed: true })
        {
            // A static class: constants only.
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var nonConstant = members.Where(member => member is not FieldInfo { IsLiteral: true }).Select(member => member.Name).ToArray();
            return nonConstant.Length == 0 ? null : $"static class with non-constant members: {string.Join(", ", nonConstant)}";
        }

        if (type.GetMethod("<Clone>$") is not null)
        {
            // A record: only the compiler's members and property accessors may have bodies.
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                          BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && !RecordMembers.Contains(method.Name))
                .Select(method => method.Name)
                .ToArray();
            return methods.Length == 0 ? null : $"record with methods: {string.Join(", ", methods)}";
        }

        return "not an interface, enum, record, or static class of constants";
    }
}
