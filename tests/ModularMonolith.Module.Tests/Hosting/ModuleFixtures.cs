using Admin.Modules;
using Admin.Modules.Data;
using Catalog.Modules;
using Catalog.Modules.Data;
using Microsoft.EntityFrameworkCore;
using Orders.Modules;
using Orders.Modules.Data;
using Reporting.Modules;
using Reporting.Modules.Data;
using SharedKernel;
using SharedKernel.Persistence;

namespace ModularMonolith.Module.Tests.Hosting;

/// <summary>
///     A module host per test class, on a database holding only that module's schema. Service tests call
///     <see cref="ResetAndSeedAsync" /> before each test: it empties the module's schema and writes the
///     module's part of <see cref="TestData" />.
/// </summary>
public abstract class ModuleFixture<TModule> : IAsyncLifetime where TModule : IModule, new()
{
    public ModuleTestHost<TModule> Host { get; private set; } = null!;

    internal string ConnectionString => Host.ConnectionString;

    /// <summary>The same database as the read-only reporting login.</summary>
    internal string ReaderConnectionString => PostgresServer.AsReportingReader(ConnectionString);

    protected abstract ModuleData Data { get; }

    /// <summary>Whether the database starts with the module's Chinook seed rows (endpoint tests) or empty.</summary>
    protected virtual bool Seeded => false;

    protected virtual TimeProvider? Time => null;

    public async Task InitializeAsync() =>
        Host = await ModuleTestHost.StartAsync<TModule>(Data, Seeded, Time);

    public async Task DisposeAsync() => await Host.DisposeAsync();

    public async Task ResetAndSeedAsync()
    {
        await Host.ResetAsync();
        await TestData.SeedAsync(this);
    }

    internal bool Has(string schema) => Data.MigratedSchemas.Contains(schema);

    internal AdministrationDbContext CreateAdministrationContext() => new(Options<AdministrationDbContext>(AdministrationDbContext.Schema));

    internal CatalogDbContext CreateCatalogContext() => new(Options<CatalogDbContext>(CatalogDbContext.Schema));

    internal OrdersDbContext CreateOrdersContext() => new(Options<OrdersDbContext>(OrdersDbContext.Schema));

    /// <summary>Reporting's context as the module uses it at runtime: connected as the read-only login.</summary>
    internal ReportingDbContext CreateReportingContext()
    {
        var builder = new DbContextOptionsBuilder<ReportingDbContext>();
        ModuleDbContextOptions.Use(builder, ReaderConnectionString, ReportingDbContext.Schema);
        return new ReportingDbContext(builder.Options);
    }

    private DbContextOptions<TContext> Options<TContext>(string schema) where TContext : DbContext
    {
        if (!Has(schema))
        {
            throw new InvalidOperationException($"The {Data.Name} host has no {schema} schema; it runs in isolation.");
        }

        var builder = new DbContextOptionsBuilder<TContext>();
        ModuleDbContextOptions.Use(builder, ConnectionString, schema);
        return builder.Options;
    }
}

public sealed class CatalogFixture : ModuleFixture<CatalogModule.Modules>
{
    protected override ModuleData Data => ModuleData.Catalog;
}

public sealed class OrdersFixture : ModuleFixture<OrdersModule.Modules>
{
    protected override ModuleData Data => ModuleData.Orders;
}

public sealed class AdministrationFixture : ModuleFixture<AdministrationModule.Modules>
{
    protected override ModuleData Data => ModuleData.Administration;
}

/// <summary>Reporting's views read the other three schemas, so its host has them too (ADR-0014).</summary>
public sealed class ReportingFixture : ModuleFixture<ReportingModule.Modules>
{
    protected override ModuleData Data => ModuleData.Reporting;
}

// Endpoint tests: the module's Chinook seed rows, as the full-host tests had.
public sealed class SeededCatalogFixture : ModuleFixture<CatalogModule.Modules>
{
    protected override ModuleData Data => ModuleData.Catalog;
    protected override bool Seeded => true;
}

public sealed class SeededOrdersFixture : ModuleFixture<OrdersModule.Modules>
{
    protected override ModuleData Data => ModuleData.Orders;
    protected override bool Seeded => true;
}

public sealed class SeededAdministrationFixture : ModuleFixture<AdministrationModule.Modules>
{
    protected override ModuleData Data => ModuleData.Administration;
    protected override bool Seeded => true;
}

public sealed class SeededReportingFixture : ModuleFixture<ReportingModule.Modules>
{
    protected override ModuleData Data => ModuleData.Reporting;
    protected override bool Seeded => true;
}
