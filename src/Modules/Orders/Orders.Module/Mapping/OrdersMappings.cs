using Orders.Modules.Domain;
using Orders.Modules.Models;

namespace Orders.Modules.Mapping;

/// <summary>Entity and API model conversions for the Orders module.</summary>
internal static class OrdersMappings
{
    public static InvoiceApiModel ToApiModel(this Invoice entity) => new()
    {
        Id = entity.Id,
        CustomerId = entity.CustomerId,
        InvoiceDate = entity.InvoiceDate,
        BillingAddress = entity.BillingAddress,
        BillingCity = entity.BillingCity,
        BillingState = entity.BillingState,
        BillingCountry = entity.BillingCountry,
        BillingPostalCode = entity.BillingPostalCode,
        Total = entity.Total,
        Status = entity.Status.ToString()
    };

    public static Invoice ToEntity(this InvoiceApiModel model) => new()
    {
        Id = model.Id,
        CustomerId = model.CustomerId,
        InvoiceDate = model.InvoiceDate,
        BillingAddress = model.BillingAddress,
        BillingCity = model.BillingCity,
        BillingState = model.BillingState,
        BillingCountry = model.BillingCountry,
        BillingPostalCode = model.BillingPostalCode,
        Total = model.Total
    };

    public static InvoiceLineApiModel ToApiModel(this InvoiceLine entity) => new()
    {
        Id = entity.Id,
        InvoiceId = entity.InvoiceId,
        TrackId = entity.TrackId,
        UnitPrice = entity.UnitPrice,
        Quantity = entity.Quantity
    };

    public static InvoiceLine ToEntity(this InvoiceLineApiModel model) => new()
    {
        Id = model.Id,
        InvoiceId = model.InvoiceId,
        TrackId = model.TrackId,
        UnitPrice = model.UnitPrice,
        Quantity = model.Quantity
    };

    public static List<InvoiceApiModel> ToApiModels(this IEnumerable<Invoice> entities) =>
        entities.Select(entity => entity.ToApiModel()).ToList();

    public static List<InvoiceLineApiModel> ToApiModels(this IEnumerable<InvoiceLine> entities) =>
        entities.Select(entity => entity.ToApiModel()).ToList();
}
