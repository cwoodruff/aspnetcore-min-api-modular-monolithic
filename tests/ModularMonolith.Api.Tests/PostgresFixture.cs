using Microsoft.EntityFrameworkCore;
using Npgsql;
using SharedKernel.Persistence;
using Testcontainers.PostgreSql;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     One PostgreSQL container per test run, holding a migrated and seeded template database.
/// </summary>
/// <remarks>
///     Every host a test boots gets its own <c>CREATE DATABASE ... TEMPLATE</c> clone, so tests can
///     write freely and test classes keep running in parallel (the per-host isolation the copied
///     SQLite file used to give). Repository tests use <see cref="CreateEmptyDatabase" /> plus
///     Respawn instead; see <see cref="RepositoryDatabaseFixture" />. The container is removed by the
///     Testcontainers resource reaper when the test process exits.
/// </remarks>
public static class PostgresFixture
{
    private const string TemplateDatabase = "chinook_template";
    private const string EmptyTemplateDatabase = "chinook_empty_template";

    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(StartAsync);

    // Two clones of the same template at once fail with "source database is being accessed".
    private static readonly SemaphoreSlim CloneLock = new(1, 1);

    public static string SeedScriptPath => Path.Combine(AppContext.BaseDirectory, DbSeeder.SeedScriptRelativePath);

    /// <summary>A new database holding the full Chinook seed.</summary>
    public static string CreateSeededDatabase() => RunSync(() => CloneAsync(TemplateDatabase));

    /// <summary>A new database with the schema applied and no rows.</summary>
    public static string CreateEmptyDatabase() => RunSync(() => CloneAsync(EmptyTemplateDatabase));

    public static AppDbContext CreateContext(string connectionString)
    {
        var options = PersistenceRegistration
            .UseAppDatabase(new DbContextOptionsBuilder<AppDbContext>(), connectionString)
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext((DbContextOptions<AppDbContext>)options);
    }

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        // Durability settings are off: the data lives only as long as the test run.
        var container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithCommand("-c", "fsync=off", "-c", "synchronous_commit=off", "-c", "full_page_writes=off",
                "-c", "max_connections=500")
            .Build();
        await container.StartAsync().ConfigureAwait(false);

        await ExecuteAsync(container, $"CREATE DATABASE {EmptyTemplateDatabase}").ConfigureAwait(false);
        await using (var empty = CreateContext(ConnectionString(container, EmptyTemplateDatabase, pooling: false)))
        {
            await empty.Database.MigrateAsync().ConfigureAwait(false);
        }

        await ExecuteAsync(container, $"CREATE DATABASE {TemplateDatabase}").ConfigureAwait(false);
        await using (var seeded = CreateContext(ConnectionString(container, TemplateDatabase, pooling: false)))
        {
            // The same path the host takes in Development.
            await DbSeeder.MigrateAndSeedAsync(seeded, SeedScriptPath).ConfigureAwait(false);
        }

        return container;
    }

    private static async Task<string> CloneAsync(string template)
    {
        var container = await Container.Value.ConfigureAwait(false);
        var name = $"test_{Guid.NewGuid():N}";

        await CloneLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await ExecuteAsync(container, $"CREATE DATABASE {name} TEMPLATE {template} STRATEGY FILE_COPY")
                .ConfigureAwait(false);
        }
        finally
        {
            CloneLock.Release();
        }

        return ConnectionString(container, name, pooling: true);
    }

    private static async Task ExecuteAsync(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString(container, "postgres", pooling: false));
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static string ConnectionString(PostgreSqlContainer container, string database, bool pooling)
    {
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = database,
            Pooling = pooling,
            // Many hosts run at once, each with its own database and pool; keep each pool small.
            MaxPoolSize = 10
        }.ToString();
    }

    // Host configuration callbacks are synchronous; run off the xUnit synchronization context.
    private static T RunSync<T>(Func<Task<T>> action) => Task.Run(action).GetAwaiter().GetResult();
}
