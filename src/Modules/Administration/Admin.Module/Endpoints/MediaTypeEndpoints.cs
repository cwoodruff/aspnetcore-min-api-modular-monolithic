using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;
using SharedKernel.Persistence;

namespace Admin.Modules.Endpoints;

public static class MediaTypeEndpoints
{
    private static readonly string[] MediaTypeTags = ["administration:mediatype", "administration:mediatype:by-id"];

    public static void MapMediaTypeEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/media-types/{id}
        group.MapGet("/media-types/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "mediatype",
                    version: "v1",
                    discriminator: $"by-id:{id}");

                var mediaType = await cache.GetOrAddAsync<object?>(key, async _ =>
                {
                    try
                    {
                        var m = await db.GetMediaType(id);
                        if (m is null) return null;
                        return m.Convert();
                    }
                    catch
                    {
                        return null;
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = MediaTypeTags
                }, ct);

                return mediaType is not null ? Results.Json(mediaType) : Results.NotFound();
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("AdministrationGetMediaTypeById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration");

        // GET /api/admin/media-types
        group.MapGet("media-types/", [Authorize] async (
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "mediatype",
                    version: "v1",
                    discriminator: "all");

                var mediaTypes = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var mediaTypeEntities = db.GetAllMediaTypes();
                        return [mediaTypeEntities.Select(m => m.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = MediaTypeTags
                }, ct);

                return Results.Json(mediaTypes);
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllMediaTypes")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration");
    }
}
