using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;
using SharedKernel.Persistence;

namespace Music.Modules.Endpoints;

public static class TrackEndpoints
{
    private static readonly string[] TrackTags = ["music:track", "music:track:by-id"];

    public static void MapTrackEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/music/tracks/{id}
        group.MapGet("/tracks/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "track",
                    version: "v1",
                    discriminator: $"by-id:{id}");

                var track = await cache.GetOrAddAsync<object?>(key, async _ =>
                {
                    try
                    {
                        var t = await db.GetTrack(id);
                        if (t is null) return null;
                        return t.Convert();
                    }
                    catch
                    {
                        return null;
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = TrackTags
                }, ct);

                return track is not null ? Results.Json(track) : Results.NotFound();
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("MusicGetTrackById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music");

        // GET /api/music/tracks
        group.MapGet("tracks/", [Authorize] async (
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "track",
                    version: "v1",
                    discriminator: "all");

                var tracks = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var trackEntities = db.GetAllTracks();
                        return [trackEntities.Select(t => t.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = TrackTags
                }, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllTracks")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music");

        // GET /api/music/tracks/artist/{id}
        group.MapGet("tracks/artist/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "track",
                    version: "v1",
                    discriminator: $"by-artist:{id}");

                var tracks = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var trackEntities = db.GetTracksByArtistId(id);
                        return [trackEntities.Select(t => t.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = TrackTags
                }, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByArtistId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music");

        // GET /api/music/tracks/playlist/{id}
        group.MapGet("tracks/playlist/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "track",
                    version: "v1",
                    discriminator: $"by-playlist:{id}");

                var tracks = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var trackEntities = db.GetTracksByPlaylistId(id);
                        return [trackEntities.Select(t => t.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = TrackTags
                }, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByPlaylistId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music");

        // GET /api/music/tracks/album/{id}
        group.MapGet("tracks/album/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "track",
                    version: "v1",
                    discriminator: $"by-album:{id}");

                var tracks = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var trackEntities = db.GetTracksByAlbumId(id);
                        return [trackEntities.Select(t => t.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = TrackTags
                }, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByAlbumId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music");

        // GET /api/music/tracks/genre/{id}
        group.MapGet("tracks/genre/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "track",
                    version: "v1",
                    discriminator: $"by-genre:{id}");

                var tracks = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var trackEntities = db.GetTracksByGenreId(id);
                        return [trackEntities.Select(t => t.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = TrackTags
                }, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByGenreId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music");

        // GET /api/music/tracks/mediatype/{id}
        group.MapGet("tracks/mediatype/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "track",
                    version: "v1",
                    discriminator: $"by-mediatype:{id}");

                var tracks = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var trackEntities = db.GetTracksByMediaTypeId(id);
                        return [trackEntities.Select(t => t.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = TrackTags
                }, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByMediaTypeId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music");

        // GET /api/music/tracks/invoice/{id}
        group.MapGet("tracks/invoice/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "music",
                    entity: "track",
                    version: "v1",
                    discriminator: $"by-invoice:{id}");

                var tracks = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var trackEntities = db.GetTracksByInvoiceId(id);
                        return [trackEntities.Select(t => t.Convert())];
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = TrackTags
                }, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByInvoiceId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music");
    }
}
