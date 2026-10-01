using SharedKernel.Events;

namespace Orders.Contracts.Events;

/// <summary>
/// Published by Orders when an invoice is finalized, through the orders outbox (ADR-0008).
/// Catalog (track sales, ADR-0009) and Administration (customer purchase summary, ADR-0010)
/// consume it eventually; neither may assume it has arrived when the finalize request returns.
/// </summary>
public sealed record InvoiceFinalized(
    Guid EventId,
    DateTimeOffset OccurredAt,
    int InvoiceId,
    int? CustomerId,
    DateTime InvoiceDate,
    decimal Total,
    IReadOnlyList<InvoiceFinalizedLine> Lines) : IIntegrationEvent;

/// <summary>One sold track on a finalized invoice.</summary>
public sealed record InvoiceFinalizedLine(int TrackId, int Quantity, decimal UnitPrice);
