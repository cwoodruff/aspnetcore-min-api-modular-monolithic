using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Events;

namespace ModularMonolith.Module.Tests.Hosting;

/// <summary>
///     Wraps a module's keyed <see cref="IEventPublisher" />: every publish is recorded, then passed to the real
///     publisher, so the outbox row is still written in the caller's transaction.
/// </summary>
internal sealed class RecordingEventPublisher(IEventPublisher inner, RecordingEventPublisher.Log log) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, DbContext sameTransactionAs, CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        log.Add(integrationEvent);
        return inner.PublishAsync(integrationEvent, sameTransactionAs, ct);
    }

    public static void Wrap(IServiceCollection services, string moduleName)
    {
        var original = services.LastOrDefault(d => d.IsKeyedService && Equals(d.ServiceKey, moduleName)
                                                                     && d.ServiceType == typeof(IEventPublisher));
        if (original?.KeyedImplementationFactory is not { } factory)
        {
            return;
        }

        services.Remove(original);
        services.AddSingleton<Log>();
        services.AddKeyedSingleton<IEventPublisher>(moduleName, (sp, key) =>
            new RecordingEventPublisher((IEventPublisher)factory(sp, key), sp.GetRequiredService<Log>()));
    }

    internal sealed class Log
    {
        private readonly List<IIntegrationEvent> _events = [];

        public IReadOnlyList<IIntegrationEvent> Events
        {
            get
            {
                lock (_events)
                {
                    return [.. _events];
                }
            }
        }

        public void Add(IIntegrationEvent integrationEvent)
        {
            lock (_events)
            {
                _events.Add(integrationEvent);
            }
        }
    }
}
