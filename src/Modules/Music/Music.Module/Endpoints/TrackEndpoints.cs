using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Music.Modules.Services;
using SharedKernel.TrafficControl;

namespace Music.Modules.Endpoints;

public static class TrackEndpoints
{
    public static void MapTrackEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/music/tracks/{id}
        group.MapGet("/tracks/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var track = await service.GetTrackByIdAsync(id, ct);

                return track is not null ? Results.Json(track) : Results.NotFound();
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("MusicGetTrackById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/tracks
        group.MapGet("tracks/", [Authorize] async (
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetAllTracksAsync(ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllTracks")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/tracks/artist/{id}
        group.MapGet("tracks/artist/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByArtistIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByArtistId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/tracks/playlist/{id}
        group.MapGet("tracks/playlist/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByPlaylistIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByPlaylistId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/tracks/album/{id}
        group.MapGet("tracks/album/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByAlbumIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByAlbumId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/tracks/genre/{id}
        group.MapGet("tracks/genre/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByGenreIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByGenreId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/tracks/mediatype/{id}
        group.MapGet("tracks/mediatype/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByMediaTypeIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByMediaTypeId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/tracks/invoice/{id}
        group.MapGet("tracks/invoice/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByInvoiceIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetTracksByInvoiceId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
