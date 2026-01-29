using Admin.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel.TrafficControl;

namespace Admin.Modules.Endpoints;

public static class CustomerEndpoints
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
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("AdministrationGetCustomerById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/admin/customers
        group.MapGet("customers/", [Authorize] async (
                ICustomerService service,
                CancellationToken ct) =>
            {
                var customers = await service.GetAllCustomersAsync(ct);

                return Results.Json(customers);
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllCustomers")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/admin/customers/support-rep/{id}
        group.MapGet("customers/support-rep/{id:int}", [Authorize] async (
                int id,
                ICustomerService service,
                CancellationToken ct) =>
            {
                var customers = await service.GetCustomersBySupportRepIdAsync(id, ct);

                return Results.Json(customers);
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("GetCustomersBySupportRepId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
