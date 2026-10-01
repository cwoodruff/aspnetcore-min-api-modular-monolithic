using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel.Diagnostics;

namespace SharedKernel.Persistence;

/// <summary>
/// The one place a module's DbContext is pointed at PostgreSQL. Every module uses the shared
/// ConnectionStrings:AppDatabase, its own schema, and a migration history table inside that schema,
/// so each module's migrations evolve independently (ADR-0003).
/// </summary>
public static class ModuleDbContextOptions
{
    public const string ConnectionName = "AppDatabase";
    public const string HistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// Registers a pooled DbContext for module <paramref name="moduleName" /> whose tables live in
    /// <paramref name="schema" />. The context is also registered as a <see cref="DbContext" /> keyed by the
    /// module name, so the host can migrate every module's context and the outbox dispatcher can find a
    /// handler's inbox without seeing the module's internal types, and it gets a health check (ADR-0013).
    /// </summary>
    /// <param name="connectionName">The connection string the module's services use.</param>
    /// <param name="migrationConnectionName">
    /// When set, the keyed <see cref="DbContext" /> (what the host migrates) uses this connection instead:
    /// for a module whose runtime login may not change the schema, such as Reporting's read-only role
    /// (ADR-0014).
    /// </param>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services,
        string moduleName, string schema, string connectionName = ConnectionName, string? migrationConnectionName = null)
        where TContext : DbContext
    {
        // Read from the provider rather than a captured configuration: sources added after registration —
        // WebApplicationFactory.ConfigureAppConfiguration, which is how the tests point the host at their
        // own database — reach only the built host's configuration.
        services.AddDbContextPool<TContext>(
            (sp, options) => Use(options, sp.GetRequiredService<IConfiguration>().GetConnectionString(connectionName), schema),
            poolSize: 128);

        if (migrationConnectionName is null || migrationConnectionName == connectionName)
        {
            services.AddKeyedScoped<DbContext>(moduleName, (sp, _) => sp.GetRequiredService<TContext>());
        }
        else
        {
            services.AddKeyedScoped<DbContext>(moduleName, (sp, _) =>
            {
                var options = new DbContextOptionsBuilder<TContext>();
                Use(options, sp.GetRequiredService<IConfiguration>().GetConnectionString(migrationConnectionName), schema);
                return ActivatorUtilities.CreateInstance<TContext>(sp, options.Options);
            });
        }

        services.AddModuleDbContextHealthCheck<TContext>(moduleName);
        return services;
    }

    public static DbContextOptionsBuilder Use(DbContextOptionsBuilder options, string? connectionString, string schema)
    {
        return options.UseNpgsql(RequireConnectionString(connectionString),
            npgsql => npgsql.MigrationsHistoryTable(HistoryTable, schema));
    }

    /// <summary>
    /// Npgsql only writes UTC values to timestamp with time zone. API payloads such as
    /// "InvoiceDate": "2024-01-01" bind as DateTimeKind.Unspecified; treat those as UTC.
    /// Call from a module DbContext's ConfigureConventions.
    /// </summary>
    public static void UseUtcDateTimes(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    /// <summary>
    /// Connectivity check for modules that own no tables yet (Identity, Reporting).
    /// </summary>
    public static async Task<bool> CanConnectAsync(IConfiguration configuration, CancellationToken ct)
    {
        try
        {
            await using var connection =
                new NpgsqlConnection(RequireConnectionString(configuration.GetConnectionString(ConnectionName)));
            await connection.OpenAsync(ct);
            return true;
        }
#pragma warning disable CA1031 // A health probe reports failure; it does not throw.
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }

    private static string RequireConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionName} is not configured. For local development run " +
                "'docker compose up -d' and use the Development environment, or set ConnectionStrings__AppDatabase.");
        }

        return connectionString;
    }

    internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Utc ? value
            : value.Kind == DateTimeKind.Local ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
