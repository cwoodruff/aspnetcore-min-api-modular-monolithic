using Npgsql;
using Respawn;
using SharedKernel.Persistence;

namespace ModularMonolith.Api.Tests;

[CollectionDefinition(Name)]
public sealed class RepositoryDatabaseDefinition : ICollectionFixture<RepositoryDatabaseFixture>
{
    public const string Name = "Repository database";
}

/// <summary>
///     An empty, migrated database shared by the repository tests, wiped with Respawn before each test.
/// </summary>
public sealed class RepositoryDatabaseFixture : IAsyncLifetime
{
    private readonly string _connectionString = PostgresFixture.CreateEmptyDatabase();
    private Respawner? _respawner;

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            SchemasToInclude = AppDbContext.Schemas.All,
            DbAdapter = DbAdapter.Postgres,
            WithReseed = true
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Empties every module schema and returns a context over the clean database.</summary>
    public async Task<AppDbContext> CreateCleanContextAsync()
    {
        await using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await _respawner!.ResetAsync(connection);
        }

        return PostgresFixture.CreateContext(_connectionString);
    }
}
