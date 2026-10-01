using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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

    /// <summary>Registers a pooled DbContext for a module whose tables live in <paramref name="schema" />.</summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext
    {
        // Read from the provider rather than a captured configuration: sources added after registration —
        // WebApplicationFactory.ConfigureAppConfiguration, which is how the tests point the host at their
        // own database — reach only the built host's configuration.
        services.AddDbContextPool<TContext>(
            (sp, options) => Use(options, sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName), schema),
            poolSize: 128);

        // Also visible as the base type, so the host can migrate every module's context in order
        // without seeing the module's internal type.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());
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
