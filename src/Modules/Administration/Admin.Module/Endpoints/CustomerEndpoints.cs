using Admin.Modules.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Admin.Modules.Endpoints;

internal static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/customers/{id}
        group.MapGet("/customers/{id:int}", CustomerHandlers.GetCustomerById)
            .RequireAdministrationReadAccess()
            .WithName("AdministrationGetCustomerById")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/customers/{id}/purchases
        group.MapGet("/customers/{id:int}/purchases", CustomerHandlers.GetCustomerPurchases)
            .RequireAdministrationReadAccess()
            .WithName("AdministrationGetCustomerPurchases")
            .WithDescription(
                "Total spent and invoice count, summed from InvoiceFinalized events. Eventually consistent: an " +
                "invoice finalized moments ago may not be counted yet.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/customers
        group.MapGet("customers/", CustomerHandlers.GetAllCustomers)
            .RequireAdministrationReadAccess()
            .WithName("GetAllCustomers")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/customers/support-rep/{id}
        group.MapGet("customers/support-rep/{id:int}", CustomerHandlers.GetCustomersBySupportRepId)
            .RequireAdministrationReadAccess()
            .WithName("GetCustomersBySupportRepId")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy
    }
}
