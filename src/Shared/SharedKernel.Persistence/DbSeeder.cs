using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SharedKernel.Persistence;

/// <summary>
/// Applies migrations and loads the Chinook seed into an empty database. The host calls it in
/// Development and Test only; other environments apply migrations as a deployment step.
/// </summary>
public static class DbSeeder
{
    public const string SeedScriptRelativePath = "data/chinook-postgres-seed.sql";

    public static async Task MigrateAndSeedAsync(AppDbContext db, string seedScriptPath, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (await db.Tracks.AnyAsync(ct))
        {
            return;
        }

        var script = await File.ReadAllTextAsync(seedScriptPath, ct);

        // Run the script as-is on the underlying connection; ExecuteSqlRaw would treat braces in
        // the data as format placeholders.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = script;
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
