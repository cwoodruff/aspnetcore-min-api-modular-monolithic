namespace SharedKernel.Events;

/// <summary>
/// Handles one integration event inside the handler's own module. The dispatcher runs it through
/// <see cref="InboxGuard" /> on the handler module's DbContext: the handler changes entities on that
/// context and must not call SaveChanges; the guard saves its work and the inbox row together.
/// Delivery is at least once, so the inbox is what makes a handler run once per event.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711",
    Justification = "Handles integration events; the name is the one the upgrade plan and ADR-0008 use.")]
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct);
}
