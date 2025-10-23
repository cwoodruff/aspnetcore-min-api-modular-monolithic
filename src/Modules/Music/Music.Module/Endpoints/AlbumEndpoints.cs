using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;
using SharedKernel.Persistence;

namespace Music.Modules.Endpoints;

public static class AlbumEndpoints
{
    private static readonly string[] AlbumTags = ["music:album", "music:album:by-id"];

    public static void MapAlbumEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/music/albums/{id}
        group.MapGet("/albums/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for this album-by-id
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "album",
                    version: "v1", // bump when response shape changes
                    discriminator: $"by-id:{id}");

                // Cache-aside: fetch from cache or query the DB on miss
                var album = await cache.GetOrAddAsync<object?>(key, async _ =>
                {
                    try
                    {
                        var a = await db.GetAlbum(id);

                        if (a is null) return null; // cache nulls? We choose not to set cache for nulls (facade skips nulls)

                        // ApiModel to avoid leaking EF tracking proxies and reduce payload
                        return a.Convert();
                    }
                    catch
                    {
                        // If the database is not initialized (e.g., missing schema), treat as not found for this demo endpoint
                        return null;
                    }
                }, new CacheEntryOptions
                {
                    // Albums are relatively static; cache for 20 minutes by default
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = AlbumTags
                }, ct);

                return album is not null ? Results.Json(album) : Results.NotFound();
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("MusicGetAlbumById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music");
    }
}
