using System.Reflection;
using System.Xml.Linq;
using static ModularMonolith.Architecture.Tests.ArchitectureConstants;

namespace ModularMonolith.Architecture.Tests;

/// <summary>
///     Catalog could leave the process (docs/extraction-playbook.md): it compiles against SharedKernel and
///     contracts only, the contract it consumes depends on nothing else, and samples/Catalog.Host builds from
///     Catalog and SharedKernel alone. Checked twice: the project references (what the build is allowed to
///     see) and the compiled assembly's references (what the code actually uses).
/// </summary>
public class ExtractionReadinessTests
{
    // Identity.Contracts is not in the plan's list: Catalog's endpoints apply its policy names (ADR-0011).
    private static readonly string[] CatalogMayReference =
        [SharedKernelAssembly, CatalogContractsAssembly, OrdersContractsAssembly, IdentityContractsAssembly];

    private static readonly string[] SolutionAssemblies =
        [.. AllModuleAssemblies, .. AllContractsAssemblies, .. AllSharedAssemblies, "ModularMonolith.Api"];

    [Fact]
    public void CatalogModule_ReferencesOnlySharedKernelAndContracts()
    {
        var projects = ProjectReferences("src/Modules/Catalog/Catalog.Module/Catalog.Module.csproj");
        Assert.Contains(SharedKernelAssembly, projects);
        Assert.Subset(CatalogMayReference.ToHashSet(), projects.ToHashSet());

        // Positive as well as negative, so a loading mistake cannot make the check vacuous.
        var compiled = SolutionReferences(typeof(Catalog.Modules.CatalogModule).Assembly);
        Assert.Contains(OrdersContractsAssembly, compiled);
        Assert.Subset(CatalogMayReference.ToHashSet(), compiled.ToHashSet());
    }

    [Theory]
    [InlineData("src/Modules/Catalog/Catalog.Contracts/Catalog.Contracts.csproj", CatalogContractsAssembly)]
    [InlineData("src/Modules/Orders/Orders.Contracts/Orders.Contracts.csproj", OrdersContractsAssembly)]
    public void Contracts_ReferenceNothingButSharedKernel(string project, string assembly)
    {
        Assert.Subset(new HashSet<string> { SharedKernelAssembly }, ProjectReferences(project).ToHashSet());
        Assert.Subset(new HashSet<string> { SharedKernelAssembly }, SolutionReferences(Assembly.Load(assembly)).ToHashSet());
    }

    [Fact]
    public void CatalogHostSample_ReferencesOnlyCatalogAndSharedKernel() =>
        Assert.Equal(
            [CatalogAssembly, SharedKernelAssembly],
            ProjectReferences("samples/Catalog.Host/Catalog.Host.csproj").Order(StringComparer.Ordinal));

    private static string[] SolutionReferences(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(name => name.Name!).Where(SolutionAssemblies.Contains)];

    private static string[] ProjectReferences(string project) =>
        [.. XDocument.Load(Path.Combine(RepositoryRoot(), project))
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ModularMonolith.Api.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("ModularMonolith.Api.sln not found above the test output.");
    }
}
