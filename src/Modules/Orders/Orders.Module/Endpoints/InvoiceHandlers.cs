using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json.Serialization;
using Orders.Modules.Domain;
using Orders.Modules.Models;
using Orders.Modules.Services;

namespace Orders.Modules.Endpoints;

/// <summary>The invoice endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class InvoiceHandlers
{
    [Authorize]
    public static async Task<Results<Ok<InvoiceApiModel>, NotFound>> GetInvoiceById(int id, IInvoiceService service, CancellationToken ct)
    {
        var invoice = await service.GetInvoiceByIdAsync(id, ct);
        return invoice is not null ? TypedResults.Ok(invoice) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<InvoiceApiModel>>> GetAllInvoices(IInvoiceService service, CancellationToken ct)
    {
        var invoices = await service.GetAllInvoicesAsync(ct);
        return TypedResults.Ok(invoices);
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<InvoiceApiModel>>> GetInvoicesByCustomerId(int id, IInvoiceService service, CancellationToken ct)
    {
        var invoices = await service.GetInvoicesByCustomerIdAsync(id, ct);
        return TypedResults.Ok(invoices);
    }

    [Authorize]
    public static async Task<Results<Accepted<InvoiceFinalizeAccepted>, Conflict, NotFound>> FinalizeInvoice(
        int id, IInvoiceService service, CancellationToken ct)
    {
        return await service.FinalizeInvoiceAsync(id, ct) switch
        {
            FinalizeInvoiceResult.Finalized => TypedResults.Accepted(
                $"/api/orders/invoices/{id}", new InvoiceFinalizeAccepted(id, "Finalized", "eventual")),
            FinalizeInvoiceResult.AlreadyFinalized => TypedResults.Conflict(),
            _ => TypedResults.NotFound()
        };
    }

    /// <summary>The 202 body; lower-case names as before the handlers moved.</summary>
    internal sealed record InvoiceFinalizeAccepted(
        [property: JsonPropertyName("invoiceId")] int InvoiceId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("salesCountersUpdate")] string SalesCountersUpdate);
}
