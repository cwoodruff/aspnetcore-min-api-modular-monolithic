using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;
using SharedKernel.Persistence;

namespace Orders.Modules.Endpoints;

public static class InvoiceLineEndpoints
{
    private static readonly string[] InvoiceLineTags = ["orders:invoiceline", "orders:invoiceline:by-id"];

    public static void MapInvoiceLineEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/orders/invoice-lines/{id}
        group.MapGet("/invoice-lines/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for this invoice-line-by-id
                var key = keys.Compose(
                    moduleName: "orders",
                    entity: "invoiceline",
                    version: "v1",
                    discriminator: $"by-id:{id}");

                var line = await cache.GetOrAddAsync<object?>(key, async _ =>
                {
                    try
                    {
                        var il = await db.GetInvoiceLine(id);
                        if (il is null) return null; // facade skips caching nulls
                        return il.Convert();
                    }
                    catch
                    {
                        // If the database is not initialized (e.g., missing schema), treat as not found for this demo endpoint
                        return null;
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = InvoiceLineTags
                }, ct);

                return line is not null ? Results.Json(line) : Results.NotFound();
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("OrdersGetInvoiceLineById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders");

        // GET /api/orders/invoice-lines
        group.MapGet("invoice-lines/", [Authorize] async (
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for the invoice-lines list
                var key = keys.Compose(
                    moduleName: "orders",
                    entity: "invoiceline",
                    version: "v1",
                    discriminator: "all");

                var lines = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var lineEntities = db.GetAllInvoiceLines();
                        return [lineEntities.Select(il => il.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = InvoiceLineTags
                }, ct);

                return Results.Json(lines);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllInvoiceLines")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders");

        // GET /api/orders/invoice-lines/invoice/{id}
        group.MapGet("invoice-lines/invoice/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "orders",
                    entity: "invoiceline",
                    version: "v1",
                    discriminator: $"by-invoice:{id}");

                var lines = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var lineEntities = db.GetInvoiceLinesByInvoiceId(id);
                        return [lineEntities.Select(il => il.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = InvoiceLineTags
                }, ct);

                return Results.Json(lines);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetInvoiceLinesByInvoiceId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders");

        // GET /api/orders/invoice-lines/track/{id}
        group.MapGet("invoice-lines/track/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "orders",
                    entity: "invoiceline",
                    version: "v1",
                    discriminator: $"by-track:{id}");

                var lines = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var lineEntities = db.GetInvoiceLinesByTrackId(id);
                        return [lineEntities.Select(il => il.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = InvoiceLineTags
                }, ct);

                return Results.Json(lines);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetInvoiceLinesByTrackId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders");
    }
}
