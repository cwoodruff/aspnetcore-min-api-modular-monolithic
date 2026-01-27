using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Modules.Services;

namespace Orders.Modules.Endpoints;

public static class InvoiceLineEndpoints
{
    public static void MapInvoiceLineEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/orders/invoice-lines/{id}
        group.MapGet("/invoice-lines/{id:int}", [Authorize] async (
                int id,
                IInvoiceLineService service,
                CancellationToken ct) =>
            {
                var line = await service.GetInvoiceLineByIdAsync(id, ct);

                return line is not null ? Results.Json(line) : Results.NotFound();
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("OrdersGetInvoiceLineById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/orders/invoice-lines
        group.MapGet("invoice-lines/", [Authorize] async (
                IInvoiceLineService service,
                CancellationToken ct) =>
            {
                var lines = await service.GetAllInvoiceLinesAsync(ct);

                return Results.Json(lines);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllInvoiceLines")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/orders/invoice-lines/invoice/{id}
        group.MapGet("invoice-lines/invoice/{id:int}", [Authorize] async (
                int id,
                IInvoiceLineService service,
                CancellationToken ct) =>
            {
                var lines = await service.GetInvoiceLinesByInvoiceIdAsync(id, ct);

                return Results.Json(lines);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetInvoiceLinesByInvoiceId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/orders/invoice-lines/track/{id}
        group.MapGet("invoice-lines/track/{id:int}", [Authorize] async (
                int id,
                IInvoiceLineService service,
                CancellationToken ct) =>
            {
                var lines = await service.GetInvoiceLinesByTrackIdAsync(id, ct);

                return Results.Json(lines);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetInvoiceLinesByTrackId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
