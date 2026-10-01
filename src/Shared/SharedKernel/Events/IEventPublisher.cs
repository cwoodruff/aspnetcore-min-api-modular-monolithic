using Microsoft.EntityFrameworkCore;
using SharedKernel.Diagnostics;

namespace SharedKernel.Events;

/// <summary>
/// Publishes by adding a row to the publishing module's outbox through the caller's own DbContext, so
/// the event commits or rolls back with the business write in the same SaveChanges. There is no
/// in-memory publish path: an event exists only once its transaction has committed.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, DbContext sameTransactionAs, CancellationToken ct)
        where TEvent : IIntegrationEvent;
}

internal sealed class OutboxEventPublisher(TimeProvider time, ModuleMeter meter) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, DbContext sameTransactionAs, CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(sameTransactionAs);

        sameTransactionAs.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = integrationEvent.EventId,
            EventType = OutboxMessage.EventTypeName(typeof(TEvent)),
            Payload = IntegrationEventSerializer.Serialize(integrationEvent),
            OccurredAt = integrationEvent.OccurredAt,
            NextAttemptAt = time.GetUtcNow()
        });
        meter.Published();

        return Task.CompletedTask;
    }
}
