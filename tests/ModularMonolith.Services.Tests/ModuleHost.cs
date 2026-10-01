using Admin.Modules;
using Catalog.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Modules;
using Reporting.Modules;
using SharedKernel;
using SharedKernel.Events;

namespace ModularMonolith.Services.Tests;

/// <summary>
///     The four data modules registered into one container, as the host registers them, without the
///     web host. The outbox dispatcher is not started; tests call it directly.
/// </summary>
internal static class ModuleHost
{
    public static ServiceProvider Build(string connectionString, TimeProvider time, string? readerConnectionString = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppDatabase"] = connectionString,
                ["ConnectionStrings:Reporting"] = readerConnectionString ?? connectionString,
                ["Outbox:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        // Registered first so the modules' TryAdd keeps the test clock.
        services.AddSingleton(time);
        services.AddReflectionJsonSerialization();

        new AdministrationModule.Modules().RegisterServices(services, configuration);
        new CatalogModule.Modules().RegisterServices(services, configuration);
        new OrdersModule.Modules().RegisterServices(services, configuration);
        new ReportingModule.Modules().RegisterServices(services, configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static IEventPublisher EventPublisher() =>
        new ServiceCollection().AddReflectionJsonSerialization().AddEventPublisher("Orders").BuildServiceProvider().GetRequiredKeyedService<IEventPublisher>("Orders");
}

/// <summary>A clock the test moves by hand, to step through retry backoff without waiting.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    // Postgres keeps microseconds; start on a whole second so stored and in-memory times compare equal.
    private DateTimeOffset _now = new(DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
