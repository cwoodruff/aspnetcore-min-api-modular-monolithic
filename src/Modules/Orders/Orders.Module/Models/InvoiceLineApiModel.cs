namespace Orders.Modules.Models;

internal sealed class InvoiceLineApiModel
{
    public int Id { get; set; }

    public int? InvoiceId { get; set; }
    public int? TrackId { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? Quantity { get; set; }
    public InvoiceApiModel? Invoice { get; set; } = null!;
}
