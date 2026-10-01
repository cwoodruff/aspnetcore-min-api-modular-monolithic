using Admin.Modules.Data;
using Catalog.Modules.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orders.Modules.Data;
using Reporting.Modules.Data;
using SharedKernel.Persistence;
using Testcontainers.PostgreSql;

namespace ModularMonolith.Module.Tests.Hosting;

/// <summary>
///     One PostgreSQL container for the whole run. Each module gets template databases holding only its own
///     schema (migrated, and optionally loaded with its slice of the Chinook seed); every module host clones
///     one, so test classes run in parallel without sharing data.
/// </summary>
public static class PostgresServer
{
    public const string ReaderLogin = "reporting_test";
    public const string ReaderPassword = "reporting_test";

    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(StartAsync);
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static readonly HashSet<string> Templates = [];

    public static string SeedScriptPath => Path.Combine(AppContext.BaseDirectory, "data", "chinook-postgres-seed.sql");

    /// <summary>A new database for <paramref name="module" />, with its schema and, if asked, its seed rows.</summary>
    public static async Task<string> CreateDatabaseAsync(ModuleData module, bool seeded)
    {
        var container = await Container.Value;
        var template = $"tpl_{module.Name.ToLowerInvariant()}_{(seeded ? "seeded" : "empty")}";
        var name = $"{module.Name.ToLowerInvariant()}_{Guid.NewGuid():N}";

        await Lock.WaitAsync();
        try
        {
            if (Templates.Add(template))
            {
                await ExecuteAsync(container, $"CREATE DATABASE {template}");
                await PrepareTemplateAsync(ConnectionString(container, template, pooling: false), module, seeded);
            }

            // Two clones of one template at once fail with "source database is being accessed".
            await ExecuteAsync(container, $"CREATE DATABASE {name} TEMPLATE {template} STRATEGY FILE_COPY");
        }
        finally
        {
            Lock.Release();
        }

        return ConnectionString(container, name, pooling: true);
    }

    /// <summary>A new, empty database (no module schema), for tests that bring their own contexts.</summary>
    public static async Task<string> CreateEmptyDatabaseAsync()
    {
        var container = await Container.Value;
        var name = $"empty_{Guid.NewGuid():N}";
        await ExecuteAsync(container, $"CREATE DATABASE {name}");
        return ConnectionString(container, name, pooling: true);
    }

    public static string AsReportingReader(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Username = ReaderLogin, Password = ReaderPassword }.ToString();

    private static async Task PrepareTemplateAsync(string connectionString, ModuleData module, bool seeded)
    {
        // Migrate in the host's order; a module that reads other schemas (Reporting) migrates them too.
        foreach (var schema in module.MigratedSchemas)
        {
            await using DbContext context = schema switch
            {
                AdministrationDbContext.Schema => new AdministrationDbContext(Options<AdministrationDbContext>(connectionString, schema)),
                CatalogDbContext.Schema => new CatalogDbContext(Options<CatalogDbContext>(connectionString, schema)),
                OrdersDbContext.Schema => new OrdersDbContext(Options<OrdersDbContext>(connectionString, schema)),
                ReportingDbContext.Schema => new ReportingDbContext(Options<ReportingDbContext>(connectionString, schema)),
                _ => throw new ArgumentOutOfRangeException(nameof(module), schema, "Unknown module schema.")
            };
            await context.Database.MigrateAsync();
        }

        if (seeded)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(ChinookSeed.For(module.MigratedSchemas), connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static DbContextOptions<TContext> Options<TContext>(string connectionString, string schema)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        ModuleDbContextOptions.Use(builder, connectionString, schema);
        return builder.Options;
    }

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        // Durability settings are off: the data lives only as long as the test run.
        var container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithCommand("-c", "fsync=off", "-c", "synchronous_commit=off", "-c", "full_page_writes=off",
                "-c", "max_connections=500")
            .Build();
        await container.StartAsync();

        // The Reporting migration grants to reporting_reader; this is a login in it (roles are server-wide).
        await ExecuteAsync(container, $"""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'reporting_reader') THEN
                    CREATE ROLE reporting_reader NOLOGIN;
                END IF;
            END $$;
            CREATE ROLE {ReaderLogin} LOGIN PASSWORD '{ReaderPassword}' IN ROLE reporting_reader;
            """);
        return container;
    }

    private static async Task ExecuteAsync(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString(container, "postgres", pooling: false));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string ConnectionString(PostgreSqlContainer container, string database, bool pooling) =>
        new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = database,
            Pooling = pooling,
            MaxPoolSize = 10
        }.ToString();
}

/// <summary>The Chinook seed script, cut down to the statements that touch the given schemas.</summary>
internal static class ChinookSeed
{
    private static readonly Lazy<string[]> Statements = new(() =>
    {
        var statements = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var line in File.ReadLines(PostgresServer.SeedScriptPath))
        {
            if (line.StartsWith("--", StringComparison.Ordinal) && current.Length == 0)
            {
                continue;
            }

            current.AppendLine(line);
            if (line.TrimEnd().EndsWith(';'))
            {
                statements.Add(current.ToString());
                current.Clear();
            }
        }

        return [.. statements];
    });

    public static string For(IReadOnlyCollection<string> schemas) =>
        string.Concat(Statements.Value.Where(statement =>
            schemas.Any(schema => statement.Contains($"INSERT INTO {schema}.", StringComparison.Ordinal)
                                  || statement.Contains($"('{schema}.", StringComparison.Ordinal))));
}
