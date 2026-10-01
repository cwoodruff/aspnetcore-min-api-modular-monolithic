using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Orders.Modules.Domain;
using Orders.Modules.Models;
using Orders.Modules.Services;

namespace Orders.Modules.Endpoints;

/// <summary>The invoiceline endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class InvoiceLineHandlers
{
    [Authorize]
    public static async Task<Results<Ok<InvoiceLineApiModel>, NotFound>> GetInvoiceLineById(int id, IInvoiceLineService service, CancellationToken ct)
    {
        var line = await service.GetInvoiceLineByIdAsync(id, ct);
        return line is not null ? TypedResults.Ok(line) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<InvoiceLineApiModel>>> GetAllInvoiceLines(IInvoiceLineService service, CancellationToken ct)
    {
        var lines = await service.GetAllInvoiceLinesAsync(ct);
        return TypedResults.Ok(lines);
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<InvoiceLineApiModel>>> GetInvoiceLinesByInvoiceId(int id, IInvoiceLineService service, CancellationToken ct)
    {
        var lines = await service.GetInvoiceLinesByInvoiceIdAsync(id, ct);
        return TypedResults.Ok(lines);
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<InvoiceLineApiModel>>> GetInvoiceLinesByTrackId(int id, IInvoiceLineService service, CancellationToken ct)
    {
        var lines = await service.GetInvoiceLinesByTrackIdAsync(id, ct);
        return TypedResults.Ok(lines);
    }
}
