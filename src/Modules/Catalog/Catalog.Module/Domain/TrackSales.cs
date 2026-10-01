namespace Catalog.Modules.Domain;

/// <summary>
/// Catalog's own running count of a track's sales, built from InvoiceFinalized events (ADR-0009).
/// Eventually consistent with Orders: it counts only invoices finalized since phase 3, after the
/// orders outbox has delivered them.
/// </summary>
internal sealed class TrackSales
{
    public int TrackId { get; set; }

    /// <summary>Units sold: the sum of line quantities.</summary>
    public int TimesSold { get; set; }

    /// <summary>The latest invoice date among the counted sales.</summary>
    public DateTime? LastSoldAt { get; set; }
}
