using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Modules.Services;
using SharedKernel.TrafficControl;

namespace Orders.Modules.Endpoints;

internal static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/orders/invoices/{id}
        group.MapGet("/invoices/{id:int}", [Authorize] async (
                int id,
                IInvoiceService service,
                CancellationToken ct) =>
            {
                var invoice = await service.GetInvoiceByIdAsync(id, ct);

                return invoice is not null ? Results.Json(invoice) : Results.NotFound();
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("OrdersGetInvoiceById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/orders/invoices
        group.MapGet("invoices/", [Authorize] async (
                IInvoiceService service,
                CancellationToken ct) =>
            {
                var invoices = await service.GetAllInvoicesAsync(ct);

                return Results.Json(invoices);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllInvoices")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/orders/invoices/customer/{id}
        group.MapGet("invoices/customer/{id:int}", [Authorize] async (
                int id,
                IInvoiceService service,
                CancellationToken ct) =>
            {
                var invoices = await service.GetInvoicesByCustomerIdAsync(id, ct);

                return Results.Json(invoices);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetInvoicesByCustomerId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // POST /api/orders/invoices/{id}/finalize
        group.MapPost("/invoices/{id:int}/finalize", [Authorize] async (
                int id,
                IInvoiceService service,
                CancellationToken ct) =>
            {
                return await service.FinalizeInvoiceAsync(id, ct) switch
                {
                    FinalizeInvoiceResult.Finalized => Results.Accepted(
                        $"/api/orders/invoices/{id}",
                        new { invoiceId = id, status = "Finalized", salesCountersUpdate = "eventual" }),
                    FinalizeInvoiceResult.AlreadyFinalized => Results.Conflict(),
                    _ => Results.NotFound()
                };
            })
            .RequireAuthorization("orders.write").RequireAuthorization("tenant.scoped")
            .WithName("OrdersFinalizeInvoice")
            .WithDescription(
                "Finalizes a draft invoice and publishes InvoiceFinalized through the orders outbox in the same " +
                "transaction. Returns 202: the invoice is final now, but track sales and the customer's purchase " +
                "summary are updated eventually, after the outbox is dispatched (salesCountersUpdate: \"eventual\"). " +
                "Do not read them back expecting this invoice to be counted yet.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithTags("Orders")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
