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

    /// <summary>
    /// Registers a module's handler for an integration event, keyed by the module (its schema name, the
    /// same key as its DbContext). Handlers are never registered unkeyed: the dispatcher resolves each
    /// module's handlers and that module's inbox context by the key, so one module's scope can never
    /// hand out another module's handler.
    /// </summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services,
        string moduleKey)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);
        services.AddKeyedScoped<IIntegrationEventHandler<TEvent>, THandler>(moduleKey);
        services.AddSingleton(new IntegrationEventSubscription(typeof(TEvent), moduleKey));
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

/// <summary>Records that the module with <paramref name="ModuleKey" /> handles <paramref name="EventType" />.</summary>
public sealed record IntegrationEventSubscription(Type EventType, string ModuleKey);
