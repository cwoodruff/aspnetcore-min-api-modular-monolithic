using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SharedKernel.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var connectionString = ResolveConnectionString();
        optionsBuilder.UseSqlite(connectionString,
            sqlite => { sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName); });
        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString()
    {
        // Prefer environment variable if provided
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return NormalizeConnectionString(fromEnv);
        }

        var dbPath = FindUsableDatabasePath() ?? Path.Combine(AppContext.BaseDirectory, "data", "chinook.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        return new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
    }

    private static string NormalizeConnectionString(string configuredConnectionString)
    {
        var builder = new SqliteConnectionStringBuilder(configuredConnectionString);
        if (!string.IsNullOrWhiteSpace(builder.DataSource))
        {
            var candidatePath = Path.IsPathRooted(builder.DataSource)
                ? builder.DataSource
                : Path.GetFullPath(builder.DataSource, AppContext.BaseDirectory);

            if (HasUsableDb(candidatePath))
            {
                builder.DataSource = candidatePath;
                return builder.ToString();
            }
        }

        var fallbackPath = FindUsableDatabasePath() ?? Path.Combine(AppContext.BaseDirectory, "data", "chinook.db");
        Directory.CreateDirectory(Path.GetDirectoryName(fallbackPath)!);
        builder.DataSource = fallbackPath;
        return builder.ToString();
    }

    private static string? FindUsableDatabasePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", "chinook.db");
            if (HasUsableDb(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool HasUsableDb(string path)
    {
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }
}
