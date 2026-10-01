namespace SharedKernel.Events;

/// <summary>
/// A fact one module publishes for others, through its outbox (ADR-0008). Event types live in the
/// publishing module's Contracts project; consumers depend on that project, never on the module.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}
