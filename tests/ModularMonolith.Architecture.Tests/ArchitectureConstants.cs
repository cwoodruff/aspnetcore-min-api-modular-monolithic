using ArchUnitNET.Loader;

namespace ModularMonolith.Architecture.Tests;

internal static class ArchitectureConstants
{
    public static readonly ArchUnitNET.Domain.Architecture MainArchitecture =
        new ArchLoader()
            .LoadAssemblies(
                typeof(Catalog.Modules.CatalogModule).Assembly,
                typeof(Orders.Modules.OrdersModule).Assembly,
                typeof(Admin.Modules.AdministrationModule).Assembly,
                typeof(Identity.Modules.IdentityModule).Assembly,
                typeof(Reporting.Modules.ReportingModule).Assembly,
                typeof(SharedKernel.IModule).Assembly,
                // Contracts assemblies hold no types yet, so they are loaded by name.
                System.Reflection.Assembly.Load(CatalogContractsAssembly),
                System.Reflection.Assembly.Load(OrdersContractsAssembly),
                System.Reflection.Assembly.Load(AdministrationContractsAssembly),
                System.Reflection.Assembly.Load(IdentityContractsAssembly)
            )
            .Build();

    public const string CatalogAssembly = "Catalog.Module";
    public const string OrdersAssembly = "Orders.Module";
    public const string AdminAssembly = "Admin.Module";
    public const string IdentityAssembly = "Identity.Module";
    public const string ReportingAssembly = "Reporting.Module";

    public const string CatalogContractsAssembly = "Catalog.Contracts";
    public const string OrdersContractsAssembly = "Orders.Contracts";
    public const string AdministrationContractsAssembly = "Administration.Contracts";
    public const string IdentityContractsAssembly = "Identity.Contracts";

    // Contracts assemblies are public by design; PublicSurfaceTests restricts only the Module assemblies.
    public static readonly string[] AllContractsAssemblies =
    [
        CatalogContractsAssembly,
        OrdersContractsAssembly,
        AdministrationContractsAssembly,
        IdentityContractsAssembly
    ];

    public const string SharedKernelAssembly = "SharedKernel";

    public static readonly string[] AllModuleAssemblies =
    [
        CatalogAssembly,
        OrdersAssembly,
        AdminAssembly,
        IdentityAssembly,
        ReportingAssembly
    ];

    public static readonly string[] AllSharedAssemblies =
    [
        SharedKernelAssembly
    ];

    // The only test assembly each module may grant InternalsVisibleTo. Null means none.
    // Catalog, Orders, Administration and (from phase 6) Reporting share the services test project until
    // per-module test hosts arrive (upgrade plan phase 8); tighten this map then, never loosen it.
    public static readonly IReadOnlyDictionary<string, string?> AllowedInternalsVisibleTo =
        new Dictionary<string, string?>
        {
            [CatalogAssembly] = "ModularMonolith.Services.Tests",
            [OrdersAssembly] = "ModularMonolith.Services.Tests",
            [AdminAssembly] = "ModularMonolith.Services.Tests",
            [IdentityAssembly] = "ModularMonolith.Api.Tests",
            [ReportingAssembly] = "ModularMonolith.Services.Tests"
        };

    /// <summary>
    /// ArchUnitNET's ResideInAssembly matches the assembly's full name ("Catalog.Module, Version=...").
    /// A short name matches no type at all, which silently turns a rule into a no-op, so every rule
    /// selects assemblies through this.
    /// </summary>
    public static string FullName(string assemblyName) =>
        System.Reflection.Assembly.Load(assemblyName).FullName
        ?? throw new InvalidOperationException($"Assembly {assemblyName} has no full name.");
}
