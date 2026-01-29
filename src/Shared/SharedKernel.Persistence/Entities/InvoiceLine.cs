using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Converters;

namespace SharedKernel.Persistence.Entities;

public class InvoiceLine : BaseEntity, IConvertModel<InvoiceLineApiModel>
{
    public int? InvoiceId { get; set; }

    public int? TrackId { get; set; }

    public decimal? UnitPrice { get; set; }

    public int? Quantity { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual Track? Track { get; set; }

    public InvoiceLineApiModel Convert()
    {
        return new InvoiceLineApiModel
        {
            Id = Id,
            InvoiceId = InvoiceId,
            TrackId = TrackId,
            UnitPrice = UnitPrice,
            Quantity = Quantity
        };
    }
}
