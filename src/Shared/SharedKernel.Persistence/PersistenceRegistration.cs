using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Persistence;

public static class PersistenceRegistration
{
    private const string ConnectionName = "AppDatabase";

    public static IServiceCollection AddKernelPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fallback: build absolute path relative to current content root
            var baseDir = AppContext.BaseDirectory;
            // Try to locate repo root by walking up until we find a 'data' folder or give up
            var current = new DirectoryInfo(baseDir);
            while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "data")))
            {
                current = current.Parent;
            }
            var root = current?.FullName ?? AppContext.BaseDirectory;
            var dbPath = Path.Combine(root, "data", "chinook.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            connectionString = $"Data Source={dbPath}";
        }

        services.AddDbContextPool<AppDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString, sqlite =>
            {
                sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });
        }, poolSize: 128);

        // services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        return services;
    }
}
