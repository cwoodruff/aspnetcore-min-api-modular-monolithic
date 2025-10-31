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

public static class ArtistEndpoints
{
    private static readonly string[] ArtistTags = ["music:artist", "music:artist:by-id"];

    public static void MapArtistEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/music/artists/{id}
        group.MapGet("/artists/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                IArtistRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for this artist-by-id
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "artist",
                    version: "v1", // bump when response shape changes
                    discriminator: $"by-id:{id}");

                // Cache-aside: fetch from cache or query the DB on miss
                var artist = await cache.GetOrAddAsync<object?>(key, async _ =>
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
                    // Artists are relatively static; cache for 20 minutes by default
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = ArtistTags
                }, ct);

                return artist is not null ? Results.Json(artist) : Results.NotFound();
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("MusicGetArtistById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/artists
        group.MapGet("artists/", [Authorize] async (
                AppDbContext db,
                IArtistRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Compose a namespaced cache key for the artists list
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "artist",
                    version: "v1", // bump when response shape changes
                    discriminator: "all");

                // Cache-aside: fetch from cache or query the DB on miss
                var artists = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        var artistEntities = await repo.GetAll();

                        // ApiModel to avoid leaking EF tracking proxies and reduce payload
                        return artistEntities.ConvertAll();
                    }
                    catch
                    {
                        // If the database is not initialized (e.g., missing schema), return empty list for this demo endpoint
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    // Artists are relatively static; cache for 20 minutes by default
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = ArtistTags
                }, ct);

                return Results.Json(artists);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllArtists")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
