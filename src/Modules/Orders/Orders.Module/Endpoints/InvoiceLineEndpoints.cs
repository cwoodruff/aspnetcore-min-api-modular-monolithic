using Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Modules.Services;

namespace Orders.Modules.Endpoints;

internal static class InvoiceLineEndpoints
{
    public static void MapInvoiceLineEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/orders/invoice-lines/{id}
        group.MapGet("/invoice-lines/{id:int}", InvoiceLineHandlers.GetInvoiceLineById)
            .RequireAuthorization(Permissions.OrdersRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("OrdersGetInvoiceLineById")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/orders/invoice-lines
        group.MapGet("invoice-lines/", InvoiceLineHandlers.GetAllInvoiceLines)
            .RequireAuthorization(Permissions.OrdersRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAllInvoiceLines")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/orders/invoice-lines/invoice/{id}
        group.MapGet("invoice-lines/invoice/{id:int}", InvoiceLineHandlers.GetInvoiceLinesByInvoiceId)
            .RequireAuthorization(Permissions.OrdersRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetInvoiceLinesByInvoiceId")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/orders/invoice-lines/track/{id}
        group.MapGet("invoice-lines/track/{id:int}", InvoiceLineHandlers.GetInvoiceLinesByTrackId)
            .RequireAuthorization(Permissions.OrdersRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetInvoiceLinesByTrackId")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy
    }
}
