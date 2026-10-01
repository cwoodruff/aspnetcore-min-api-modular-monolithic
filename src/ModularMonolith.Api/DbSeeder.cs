using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ModularMonolith.Api;

/// <summary>
/// Applies every module's migrations, then loads the Chinook seed into an empty database. The host
/// calls it in Development and Test only; other environments apply migrations as a deployment step.
/// </summary>
public static class DbSeeder
{
    public const string SeedScriptRelativePath = "data/chinook-postgres-seed.sql";

    // Schemas in migration order. Each module's history table lives in its own schema (ADR-0003).
    // Reporting joins this list in phase 6.
    private static readonly string[] MigrationOrder = ["administration", "catalog", "orders"];

    public static async Task MigrateAndSeedAsync(IServiceProvider services, string seedScriptPath,
        CancellationToken ct = default)
    {
        var contexts = services.GetServices<DbContext>()
            .OrderBy(context => Array.IndexOf(MigrationOrder, context.Model.GetDefaultSchema()))
            .ToArray();

        var unknown = contexts.Where(context => !MigrationOrder.Contains(context.Model.GetDefaultSchema())).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidOperationException(
                "No migration order for: " + string.Join(", ", unknown.Select(context => context.GetType().Name)));
        }

        foreach (var context in contexts)
        {
            await context.Database.MigrateAsync(ct);
        }

        // The seed spans all three schemas; any context's connection reaches the shared database.
        var database = contexts[0].Database;
        var hasTracks = await database
            .SqlQueryRaw<bool>("""SELECT EXISTS (SELECT 1 FROM catalog."Track") AS "Value" """)
            .SingleAsync(ct);
        if (hasTracks)
        {
            return;
        }

        var script = await File.ReadAllTextAsync(seedScriptPath, ct);

        // Run the script as-is on the underlying connection; ExecuteSqlRaw would treat braces in
        // the data as format placeholders.
        await using var transaction = await database.BeginTransactionAsync(ct);
        await using (var command = database.GetDbConnection().CreateCommand())
        {
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = script;
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
