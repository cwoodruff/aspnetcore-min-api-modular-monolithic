using SharedKernel.Persistence.Converters;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.ApiModels;

public sealed class InvoiceLineApiModel : BaseApiModel, IConvertModel<InvoiceLine>
{
    public int? InvoiceId { get; set; }
    public int? TrackId { get; set; }
    public string? TrackName { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? Quantity { get; set; }
    public InvoiceApiModel? Invoice { get; set; } = null!;

    public TrackApiModel? Track { get; set; } = null!;

    public InvoiceLine Convert()
    {
        return new InvoiceLine
        {
            Id = Id,
            InvoiceId = InvoiceId,
            TrackId = TrackId,
            UnitPrice = UnitPrice,
            Quantity = Quantity
        };
    }
}
