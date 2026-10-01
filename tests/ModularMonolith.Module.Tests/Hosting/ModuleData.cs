using Admin.Modules.Data;
using Catalog.Modules.Data;
using Orders.Modules.Data;
using Reporting.Modules.Data;

namespace ModularMonolith.Module.Tests.Hosting;

/// <summary>
///     What a module's host needs from the database: the schemas to migrate and to reset. Each module has only
///     its own, except Reporting, whose views read the other three schemas (ADR-0014).
/// </summary>
public sealed record ModuleData(string Name, IReadOnlyList<string> MigratedSchemas)
{
    public static readonly ModuleData Catalog = new("Catalog", [CatalogDbContext.Schema]);
    public static readonly ModuleData Orders = new("Orders", [OrdersDbContext.Schema]);
    public static readonly ModuleData Administration = new("Administration", [AdministrationDbContext.Schema]);

    public static readonly ModuleData Reporting = new("Reporting",
        [AdministrationDbContext.Schema, CatalogDbContext.Schema, OrdersDbContext.Schema, ReportingDbContext.Schema]);

    public static readonly ModuleData Identity = new("Identity", []);

    /// <summary>samples/Catalog.Host: Catalog's schema, and Orders' for the outbox it reads (ADR-0017).</summary>
    public static readonly ModuleData CatalogHost = new("CatalogHost", [CatalogDbContext.Schema, OrdersDbContext.Schema]);
}
