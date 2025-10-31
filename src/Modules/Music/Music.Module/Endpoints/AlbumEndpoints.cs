using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

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
                IAlbumRepository repo,
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
                        var a = await repo.GetById(id);
                        return a;
                    }
                    catch(Exception ex)
                    {
                        // If the database is not initialized (e.g., missing schema), treat as not found for this demo endpoint
                        return ex.Message;
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
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/albums
        group.MapGet("albums/", [Authorize] async (
                AppDbContext db,
                IAlbumRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for the albums list
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "album",
                    version: "v1", // bump when response shape changes
                    discriminator: "all");

                // Cache-aside: fetch from cache or query the DB on miss
                var albums = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        var albumEntities = await repo.GetAll();

                        // ApiModel to avoid leaking EF tracking proxies and reduce payload
                        //return [albumEntities.Select(a => a.Convert())];
                        return albumEntities.ConvertAll();
                    }
                    catch
                    {
                        // If the database is not initialized (e.g., missing schema), return empty list for this demo endpoint
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    // Albums are relatively static; cache for 20 minutes by default
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = AlbumTags
                }, ct);

                return Results.Json(albums);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllAlbums")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        //
        group.MapGet("albums/artist/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                IAlbumRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for the albums list by artist
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "album",
                    version: "v1", // bump when response shape changes
                    discriminator: $"by-artist:{id}");

                // Cache-aside: fetch from cache or query the DB on miss
                var albums = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        var albumEntities = await repo.GetByArtistId(id);

                        // ApiModel to avoid leaking EF tracking proxies and reduce payload
                        return albumEntities.ConvertAll();
                    }
                    catch
                    {
                        // If the database is not initialized (e.g., missing schema), return empty list for this demo endpoint
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    // Albums are relatively static; cache for 20 minutes by default
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = AlbumTags
                }, ct);

                return Results.Json(albums);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAlbumsByArtistId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
