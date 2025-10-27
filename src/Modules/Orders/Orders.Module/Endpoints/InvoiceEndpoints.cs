using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;
using SharedKernel.Persistence;

namespace Orders.Modules.Endpoints;

public static class InvoiceEndpoints
{
    private static readonly string[] InvoiceTags = ["orders:invoice", "orders:invoice:by-id"];

    public static void MapInvoiceEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/orders/invoices/{id}
        group.MapGet("/invoices/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for this invoice-by-id
                var key = keys.Compose(
                    moduleName: "orders",
                    entity: "invoice",
                    version: "v1",
                    discriminator: $"by-id:{id}");

                var invoice = await cache.GetOrAddAsync<object?>(key, async _ =>
                {
                    try
                    {
                        var i = await db.GetInvoice(id);
                        if (i is null) return null; // facade skips caching nulls
                        return i.Convert();
                    }
                    catch
                    {
                        // If the database is not initialized (e.g., missing schema), treat as not found for this demo endpoint
                        return null;
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = InvoiceTags
                }, ct);

                return invoice is not null ? Results.Json(invoice) : Results.NotFound();
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("OrdersGetInvoiceById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders");

        // GET /api/orders/invoices
        group.MapGet("invoices/", [Authorize] async (
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for the invoices list
                var key = keys.Compose(
                    moduleName: "orders",
                    entity: "invoice",
                    version: "v1",
                    discriminator: "all");

                var invoices = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var invoiceEntities = db.GetAllInvoices();
                        return [invoiceEntities.Select(i => i.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = InvoiceTags
                }, ct);

                return Results.Json(invoices);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllInvoices")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders");

        // GET /api/orders/invoices/customer/{id}
        group.MapGet("invoices/customer/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "orders",
                    entity: "invoice",
                    version: "v1",
                    discriminator: $"by-customer:{id}");

                var invoices = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var invoiceEntities = db.GetInvoicesByCustomerId(id);
                        return [invoiceEntities.Select(i => i.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = InvoiceTags
                }, ct);

                return Results.Json(invoices);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetInvoicesByCustomerId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders");

        // GET /api/orders/invoices/employee/{id}
        group.MapGet("invoices/employee/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "orders",
                    entity: "invoice",
                    version: "v1",
                    discriminator: $"by-employee:{id}");

                var invoices = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var invoiceEntities = db.GetInvoicesByEmployeeId(id);
                        return [invoiceEntities.Select(i => i.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = InvoiceTags
                }, ct);

                return Results.Json(invoices);
            })
            .RequireAuthorization("orders.read").RequireAuthorization("tenant.scoped")
            .WithName("GetInvoicesByEmployeeId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Orders");
    }
}
