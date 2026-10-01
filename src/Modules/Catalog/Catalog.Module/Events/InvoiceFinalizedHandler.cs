using Catalog.Modules.Data;
using Catalog.Modules.Domain;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.Events;
using SharedKernel.Events;

namespace Catalog.Modules.Events;

/// <summary>
/// Adds a finalized invoice's lines to each track's sales. Runs inside the catalog inbox guard, which
/// saves these changes with the inbox row; it must not call SaveChanges itself.
/// </summary>
internal sealed class InvoiceFinalizedHandler(CatalogDbContext db) : IIntegrationEventHandler<InvoiceFinalized>
{
    public async Task HandleAsync(InvoiceFinalized integrationEvent, CancellationToken ct)
    {
        foreach (var track in integrationEvent.Lines.GroupBy(line => line.TrackId))
        {
            var sales = await db.TrackSales.SingleOrDefaultAsync(s => s.TrackId == track.Key, ct);
            if (sales is null)
            {
                sales = new TrackSales { TrackId = track.Key };
                db.TrackSales.Add(sales);
            }

            sales.TimesSold += track.Sum(line => line.Quantity);
            if (sales.LastSoldAt is null || integrationEvent.InvoiceDate > sales.LastSoldAt)
            {
                sales.LastSoldAt = integrationEvent.InvoiceDate;
            }
        }
    }
}
