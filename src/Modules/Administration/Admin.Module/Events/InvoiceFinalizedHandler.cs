using Admin.Modules.Data;
using Admin.Modules.Domain;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Events;
using SharedKernel.Events;

namespace Admin.Modules.Events;

/// <summary>
/// Adds a finalized invoice to its customer's purchase summary. Runs inside the administration inbox
/// guard, which saves these changes with the inbox row; it must not call SaveChanges itself.
/// </summary>
internal sealed class InvoiceFinalizedHandler(AdministrationDbContext db) : IIntegrationEventHandler<InvoiceFinalized>
{
    public async Task HandleAsync(InvoiceFinalized integrationEvent, CancellationToken ct)
    {
        if (integrationEvent.CustomerId is not { } customerId)
        {
            return;
        }

        var summary = await db.CustomerPurchaseSummaries.SingleOrDefaultAsync(s => s.CustomerId == customerId, ct);
        if (summary is null)
        {
            summary = new CustomerPurchaseSummary { CustomerId = customerId };
            db.CustomerPurchaseSummaries.Add(summary);
        }

        summary.TotalSpent += integrationEvent.Total;
        summary.InvoiceCount++;
        if (summary.LastPurchaseAt is null || integrationEvent.InvoiceDate > summary.LastPurchaseAt)
        {
            summary.LastPurchaseAt = integrationEvent.InvoiceDate;
        }
    }
}
