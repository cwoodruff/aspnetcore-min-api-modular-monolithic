using Admin.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Admin.Modules.Endpoints;

internal static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/customers/{id}
        group.MapGet("/customers/{id:int}", [Authorize] async (
                int id,
                ICustomerService service,
                CancellationToken ct) =>
            {
                var customer = await service.GetCustomerByIdAsync(id, ct);

                return customer is not null ? Results.Json(customer) : Results.NotFound();
            })
            .RequireAdministrationReadAccess()
            .WithName("AdministrationGetCustomerById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/customers/{id}/purchases
        group.MapGet("/customers/{id:int}/purchases", [Authorize] async (
                int id,
                ICustomerService service,
                CancellationToken ct) =>
            {
                var purchases = await service.GetCustomerPurchasesAsync(id, ct);

                return purchases is not null ? Results.Json(purchases) : Results.NotFound();
            })
            .RequireAdministrationReadAccess()
            .WithName("AdministrationGetCustomerPurchases")
            .WithDescription(
                "Total spent and invoice count, summed from InvoiceFinalized events. Eventually consistent: an " +
                "invoice finalized moments ago may not be counted yet.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/customers
        group.MapGet("customers/", [Authorize] async (
                ICustomerService service,
                CancellationToken ct) =>
            {
                var customers = await service.GetAllCustomersAsync(ct);

                return Results.Json(customers);
            })
            .RequireAdministrationReadAccess()
            .WithName("GetAllCustomers")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/customers/support-rep/{id}
        group.MapGet("customers/support-rep/{id:int}", [Authorize] async (
                int id,
                ICustomerService service,
                CancellationToken ct) =>
            {
                var customers = await service.GetCustomersBySupportRepIdAsync(id, ct);

                return Results.Json(customers);
            })
            .RequireAdministrationReadAccess()
            .WithName("GetCustomersBySupportRepId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy
    }
}
