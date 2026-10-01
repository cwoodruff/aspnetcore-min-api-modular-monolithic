using Admin.Modules.Data;
using Catalog.Modules.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orders.Modules.Data;
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
        [AdministrationDbContext.Schema, CatalogDbContext.Schema, OrdersDbContext.Schema];

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithCommand("-c", "fsync=off", "-c", "synchronous_commit=off", "-c", "full_page_writes=off")
        .Build();

    private Respawner? _respawner;

    private string ConnectionString => _container.GetConnectionString();

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

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
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

    internal OrdersDbContext CreateOrdersContext() =>
        new(Options<OrdersDbContext>(OrdersDbContext.Schema));

    private DbContextOptions<TContext> Options<TContext>(string schema) where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        ModuleDbContextOptions.Use(builder, ConnectionString, schema);
        return builder.Options;
    }
}
