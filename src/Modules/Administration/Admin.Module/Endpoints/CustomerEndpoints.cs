using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;
using SharedKernel.Persistence;

namespace Admin.Modules.Endpoints;

public static class CustomerEndpoints
{
    private static readonly string[] CustomerTags = ["administration:customer", "administration:customer:by-id"];

    public static void MapCustomerEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/administration/customers/{id}
        group.MapGet("/customers/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "customer",
                    version: "v1",
                    discriminator: $"by-id:{id}");

                var customer = await cache.GetOrAddAsync<object?>(key, async _ =>
                {
                    try
                    {
                        var c = await db.GetCustomer(id);
                        if (c is null) return null;
                        return c.Convert();
                    }
                    catch
                    {
                        return null;
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = CustomerTags
                }, ct);

                return customer is not null ? Results.Json(customer) : Results.NotFound();
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("AdministrationGetCustomerById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration");

        // GET /api/administration/customers
        group.MapGet("customers/", [Authorize] async (
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "customer",
                    version: "v1",
                    discriminator: "all");

                var customers = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        var customerEntities = await db.GetAllCustomers();
                        return [customerEntities.Select(c => c.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = CustomerTags
                }, ct);

                return Results.Json(customers);
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllCustomers")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration");

        // GET /api/administration/customers/support-rep/{id}
        group.MapGet("customers/support-rep/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "customer",
                    version: "v1",
                    discriminator: $"by-supportrep:{id}");

                var customers = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        var customerEntities = await db.GetCustomerBySupportRepId(id);
                        return [customerEntities.Select(c => c.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = CustomerTags
                }, ct);

                return Results.Json(customers);
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("GetCustomersBySupportRepId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration");
    }
}
