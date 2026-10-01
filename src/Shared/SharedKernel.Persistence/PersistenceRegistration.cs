using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Persistence.Validation;

namespace SharedKernel.Persistence;

public static class PersistenceRegistration
{
    public const string ConnectionName = "AppDatabase";

    public static IServiceCollection AddKernelPersistence(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContextPool<AppDbContext>(
            (sp, options) =>
            {
                // Read from the provider rather than the captured configuration: sources added after
                // registration — WebApplicationFactory.ConfigureAppConfiguration, which is how the
                // tests point the host at their own database — reach only the built host's configuration.
                var hostConfiguration = sp.GetService<IConfiguration>() ?? configuration;
                UseAppDatabase(options, hostConfiguration.GetConnectionString(ConnectionName));
            }, 128);

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<CustomerValidator>();

        return services;
    }

    /// <summary>
    /// The single place the provider is chosen, shared by the host, the design-time factory and tests.
    /// </summary>
    public static DbContextOptionsBuilder UseAppDatabase(DbContextOptionsBuilder options, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionName} is not configured. For local development run " +
                "'docker compose up -d' and use the Development environment, or set ConnectionStrings__AppDatabase.");
        }

        return options.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
    }
}
