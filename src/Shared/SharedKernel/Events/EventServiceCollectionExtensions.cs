using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Diagnostics;

namespace SharedKernel.Events;

public static class EventServiceCollectionExtensions
{
    /// <summary>
    /// For a module that publishes events through its outbox: an <see cref="IEventPublisher" /> keyed by
    /// module name, counting on the module's meter.
    /// </summary>
    public static IServiceCollection AddEventPublisher(this IServiceCollection services, string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        services.TryAddSingleton(TimeProvider.System);
        services.AddModuleMeter(moduleName);
        services.AddKeyedSingleton<IEventPublisher>(moduleName, (sp, _) => new OutboxEventPublisher(
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredKeyedService<ModuleMeter>(moduleName),
            sp.GetRequiredKeyedService<JsonSerializerOptions>(ModuleJson.OptionsKey)));
        return services;
    }

    /// <summary>
    /// Registers a module's handler for an integration event, keyed by the module name (the same key as
    /// its DbContext). Handlers are never registered unkeyed: the dispatcher resolves each
    /// module's handlers and that module's inbox context by the key, so one module's scope can never
    /// hand out another module's handler.
    /// </summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services, string moduleKey)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);
        services.AddKeyedScoped<THandler>(moduleKey);
        services.AddKeyedScoped<IIntegrationEventHandler<TEvent>>(moduleKey,
            (sp, key) => sp.GetRequiredKeyedService<THandler>(key));

        // The delegate is built here, where both types are known, so the dispatcher needs no reflection.
        services.AddSingleton(new IntegrationEventSubscription(typeof(TEvent), moduleKey, typeof(THandler),
            (sp, integrationEvent, ct) => sp.GetRequiredKeyedService<THandler>(moduleKey).HandleAsync((TEvent)integrationEvent, ct)));
        return services;
    }

    /// <summary>
    /// Registers a module's outbox dispatcher as a singleton and as a hosted service, with the module's
    /// work queue it runs its batches through.
    /// </summary>
    public static IServiceCollection AddOutboxDispatcher<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDispatcher>(
        this IServiceCollection services, string moduleName)
        where TDispatcher : OutboxDispatcher
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddModuleWorkQueue(moduleName);
        services.AddSingleton<TDispatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<TDispatcher>());
        return services;
    }
}

/// <summary>
/// Records that the module with <paramref name="ModuleKey" /> handles <paramref name="EventType" /> with
/// <paramref name="HandlerType" />; <paramref name="Handle" /> resolves the handler from a scope and calls it.
/// </summary>
public sealed record IntegrationEventSubscription(
    Type EventType,
    string ModuleKey,
    Type HandlerType,
    Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task> Handle);
