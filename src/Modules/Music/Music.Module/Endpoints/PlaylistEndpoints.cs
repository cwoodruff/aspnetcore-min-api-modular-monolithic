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

public static class PlaylistEndpoints
{
    private static readonly string[] PlaylistTags = ["music:playlist", "music:playlist:by-id"];

    public static void MapPlaylistEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/music/playlists/{id}
        group.MapGet("/playlists/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                IPlaylistRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for this playlist-by-id
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "playlist",
                    version: "v1", // bump when response shape changes
                    discriminator: $"by-id:{id}");

                // Cache-aside: fetch from cache or query the DB on miss
                var playlist = await cache.GetOrAddAsync<SharedKernel.Persistence.ApiModels.PlaylistApiModel?>(key, async _ =>
                {
                    try
                    {
                        var p = await repo.GetById(id);
                        return p;
                    }
                    catch
                    {
                        // Treat exceptions (e.g., not found) as null to avoid caching error strings
                        return null;
                    }
                }, new CacheEntryOptions
                {
                    // Playlists are relatively static; cache for 20 minutes by default
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = PlaylistTags
                }, ct);

                return playlist is not null ? TypedResults.Ok(playlist) : Results.NotFound();
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("MusicGetPlaylistById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/playlists
        group.MapGet("playlists/", [Authorize] async (
                AppDbContext db,
                IPlaylistRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for the playlists list
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "playlist",
                    version: "v1", // bump when response shape changes
                    discriminator: "all");

                // Cache-aside: fetch from cache or query the DB on miss
                var playlists = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        // Ensure async path
                        await Task.Yield();
                        var playlistEntities = await repo.GetAll();

                        // ApiModel to avoid leaking EF tracking proxies and reduce payload
                        return playlistEntities.ConvertAll();
                    }
                    catch
                    {
                        // If the database is not initialized (e.g., missing schema), return empty list for this demo endpoint
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    // Playlists are relatively static; cache for 20 minutes by default
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = PlaylistTags
                }, ct);

                return Results.Json(playlists);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllPlaylists")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
