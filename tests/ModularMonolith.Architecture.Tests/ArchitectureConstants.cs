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
                typeof(SharedKernel.Persistence.AppDbContext).Assembly,
                typeof(SharedKernel.DataSQLite.Repositories.BaseRepository<>).Assembly
            )
            .Build();

    public const string CatalogAssembly = "Catalog.Module";
    public const string OrdersAssembly = "Orders.Module";
    public const string AdminAssembly = "Admin.Module";
    public const string IdentityAssembly = "Identity.Module";
    public const string ReportingAssembly = "Reporting.Module";

    public const string SharedKernelAssembly = "SharedKernel";
    public const string SharedKernelPersistenceAssembly = "SharedKernel.Persistence";
    public const string SharedKernelDataSQLiteAssembly = "SharedKernel.DataSQLite";

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
        SharedKernelAssembly,
        SharedKernelPersistenceAssembly,
        SharedKernelDataSQLiteAssembly
    ];

    // The only test assembly each module may grant InternalsVisibleTo. Null means none.
    // Catalog, Orders and Administration share the services test project until per-module
    // test hosts arrive (upgrade plan phase 8); tighten this map then, never loosen it.
    public static readonly IReadOnlyDictionary<string, string?> AllowedInternalsVisibleTo =
        new Dictionary<string, string?>
        {
            [CatalogAssembly] = "ModularMonolith.Services.Tests",
            [OrdersAssembly] = "ModularMonolith.Services.Tests",
            [AdminAssembly] = "ModularMonolith.Services.Tests",
            [IdentityAssembly] = "ModularMonolith.Api.Tests",
            [ReportingAssembly] = null
        };
}
