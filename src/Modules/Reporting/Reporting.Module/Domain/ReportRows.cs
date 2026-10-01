namespace Reporting.Modules.Domain;

/// <summary>A row of reporting.sales_by_genre (keyless; ADR-0014).</summary>
internal sealed class SalesByGenreRow
{
    public int GenreId { get; set; }

    public string? GenreName { get; set; }

    public long TrackCount { get; set; }

    public long UnitsSold { get; set; }
}

/// <summary>A row of reporting.invoice_lines_with_names (keyless; ADR-0014).</summary>
internal sealed class InvoiceLineWithNamesRow
{
    public int InvoiceLineId { get; set; }

    public int InvoiceId { get; set; }

    public int? TrackId { get; set; }

    public string? TrackName { get; set; }

    public int? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public decimal? UnitPrice { get; set; }

    public int? Quantity { get; set; }
}
