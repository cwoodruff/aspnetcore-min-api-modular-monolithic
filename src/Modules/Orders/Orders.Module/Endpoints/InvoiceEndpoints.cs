using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Modules.Services;

namespace Orders.Modules.Endpoints;

public static class InvoiceEndpoints
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
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

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
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

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
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
