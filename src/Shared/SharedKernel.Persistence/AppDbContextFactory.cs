using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SharedKernel.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var connectionString = ResolveConnectionString();
        optionsBuilder.UseSqlite(connectionString, sqlite =>
        {
            sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
        });
        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString()
    {
        // Prefer environment variable if provided
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        // Otherwise build absolute path to data/chinook.db by walking up to repo root
        var baseDir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(baseDir);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "data")))
        {
            current = current.Parent;
        }
        var root = current?.FullName ?? AppContext.BaseDirectory;
        var dbPath = Path.Combine(root, "data", "chinook.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        return $"Data Source={dbPath}";
    }
}
