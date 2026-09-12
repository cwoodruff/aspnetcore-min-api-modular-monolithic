using FluentValidation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Persistence.Validation;

namespace SharedKernel.Persistence;

public static class PersistenceRegistration
{
    private const string ConnectionName = "AppDatabase";

    public static IServiceCollection AddKernelPersistence(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContextPool<AppDbContext>(
            (sp, options) =>
            {
                // Read from the provider rather than the captured configuration: sources added after
                // registration — WebApplicationFactory.ConfigureAppConfiguration, which is how the
                // tests point the host at their own copy — reach only the built host's configuration.
                var hostConfiguration = sp.GetService<IConfiguration>() ?? configuration;
                var connectionString =
                    ResolveConnectionString(hostConfiguration.GetConnectionString(ConnectionName));

                options.UseSqlite(connectionString,
                    sqlite => { sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName); });
            }, 128);

        // services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<CustomerValidator>();

        return services;
    }

    private static string ResolveConnectionString(string? configuredConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
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
        }

        var dbPath = FindUsableDatabasePath() ?? Path.Combine(AppContext.BaseDirectory, "data", "chinook.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        return new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
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
