using ArchUnitNET.Loader;

namespace ModularMonolith.Architecture.Tests;

internal static class ArchitectureConstants
{
    public static readonly ArchUnitNET.Domain.Architecture MainArchitecture =
        new ArchLoader()
            .LoadAssemblies(
                typeof(Music.Modules.MusicModule).Assembly,
                typeof(Orders.Modules.OrdersModule).Assembly,
                typeof(Admin.Modules.AdministrationModule).Assembly,
                typeof(Identity.Modules.IdentityModule).Assembly,
                typeof(Reporting.Modules.ReportingModule).Assembly,
                typeof(SharedKernel.IModule).Assembly,
                typeof(SharedKernel.Persistence.AppDbContext).Assembly,
                typeof(SharedKernel.DataSQLite.Repositories.BaseRepository<>).Assembly
            )
            .Build();

    public const string MusicAssembly = "Music.Module";
    public const string OrdersAssembly = "Orders.Module";
    public const string AdminAssembly = "Admin.Module";
    public const string IdentityAssembly = "Identity.Module";
    public const string ReportingAssembly = "Reporting.Module";

    public const string SharedKernelAssembly = "SharedKernel";
    public const string SharedKernelPersistenceAssembly = "SharedKernel.Persistence";
    public const string SharedKernelDataSQLiteAssembly = "SharedKernel.DataSQLite";

    public static readonly string[] AllModuleAssemblies =
    [
        MusicAssembly,
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
}
