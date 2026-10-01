using Admin.Modules.Data;
using Catalog.Modules.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orders.Modules.Data;
using Reporting.Modules.Data;
using Respawn;
using SharedKernel.Persistence;
using Testcontainers.PostgreSql;

namespace ModularMonolith.Services.Tests;

[CollectionDefinition(Name)]
public sealed class ModuleDatabaseDefinition : ICollectionFixture<ModuleDatabaseFixture>
{
    public const string Name = "Module database";
}

/// <summary>
///     One PostgreSQL container with every module's migrations applied, the way the host applies them.
///     Each test starts from <see cref="ResetAndSeedAsync" />: Respawn empties the module schemas and
///     <see cref="TestData" /> writes a small graph through each module's own context.
/// </summary>
public sealed class ModuleDatabaseFixture : IAsyncLifetime
{
    private static readonly string[] ModuleSchemas =
        [AdministrationDbContext.Schema, CatalogDbContext.Schema, OrdersDbContext.Schema, ReportingDbContext.Schema];

    // A login in the read-only reporting_reader role the Reporting migration grants to (ADR-0014).
    private const string ReaderLogin = "reporting_test";
    private const string ReaderPassword = "reporting_test";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithCommand("-c", "fsync=off", "-c", "synchronous_commit=off", "-c", "full_page_writes=off")
        .Build();

    private Respawner? _respawner;

    internal string ConnectionString => _container.GetConnectionString();

    /// <summary>The same database as the read-only reporting login.</summary>
    internal string ReaderConnectionString => new NpgsqlConnectionStringBuilder(ConnectionString)
    {
        Username = ReaderLogin,
        Password = ReaderPassword
    }.ToString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Same order as the host's DbSeeder.
        await using (var administration = CreateAdministrationContext())
        {
            await administration.Database.MigrateAsync();
        }

        await using (var catalog = CreateCatalogContext())
        {
            await catalog.Database.MigrateAsync();
        }

        await using (var orders = CreateOrdersContext())
        {
            await orders.Database.MigrateAsync();
        }

        // Reporting migrates as the owner; its runtime login only reads (ADR-0014).
        await using (var reporting = new ReportingDbContext(Options<ReportingDbContext>(ReportingDbContext.Schema)))
        {
            await reporting.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using (var login = new NpgsqlCommand(
                         $"CREATE ROLE {ReaderLogin} LOGIN PASSWORD '{ReaderPassword}' IN ROLE reporting_reader", connection))
        {
            await login.ExecuteNonQueryAsync();
        }

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            SchemasToInclude = ModuleSchemas,
            // Each schema's migration history is part of the schema; keep it.
            TablesToIgnore = [new Respawn.Graph.Table(ModuleDbContextOptions.HistoryTable)],
            DbAdapter = DbAdapter.Postgres,
            WithReseed = true
        });
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Empties the module schemas and writes <see cref="TestData" />'s graph.</summary>
    public async Task ResetAndSeedAsync()
    {
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await _respawner!.ResetAsync(connection);
        }

        await TestData.SeedAsync(this);
    }

    internal AdministrationDbContext CreateAdministrationContext() =>
        new(Options<AdministrationDbContext>(AdministrationDbContext.Schema));

    internal CatalogDbContext CreateCatalogContext() =>
        new(Options<CatalogDbContext>(CatalogDbContext.Schema));

    /// <summary>Reporting's context as the module uses it at runtime: connected as the read-only login.</summary>
    internal ReportingDbContext CreateReportingContext()
    {
        var builder = new DbContextOptionsBuilder<ReportingDbContext>();
        ModuleDbContextOptions.Use(builder, ReaderConnectionString, ReportingDbContext.Schema);
        return new ReportingDbContext(builder.Options);
    }

    internal OrdersDbContext CreateOrdersContext() =>
        new(Options<OrdersDbContext>(OrdersDbContext.Schema));

    private DbContextOptions<TContext> Options<TContext>(string schema) where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        ModuleDbContextOptions.Use(builder, ConnectionString, schema);
        return builder.Options;
    }
}
