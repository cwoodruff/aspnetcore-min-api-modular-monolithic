using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SharedKernel.Events;

public static class EventServiceCollectionExtensions
{
    /// <summary>For a module that publishes events through its outbox.</summary>
    public static IServiceCollection AddEventPublisher(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IEventPublisher, OutboxEventPublisher>();
        return services;
    }

    /// <summary>Registers a module's outbox dispatcher as a singleton and as a hosted service.</summary>
    public static IServiceCollection AddOutboxDispatcher<TDispatcher>(this IServiceCollection services)
        where TDispatcher : OutboxDispatcher
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<TDispatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<TDispatcher>());
        return services;
    }
}
