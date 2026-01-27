using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Music.Modules.Services;

namespace Music.Modules.Endpoints;

public static class PlaylistEndpoints
{
    public static void MapPlaylistEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/music/playlists/{id}
        group.MapGet("/playlists/{id:int}", [Authorize] async (
                int id,
                IPlaylistService service,
                CancellationToken ct) =>
            {
                var playlist = await service.GetPlaylistByIdAsync(id, ct);

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
                IPlaylistService service,
                CancellationToken ct) =>
            {
                var playlists = await service.GetAllPlaylistsAsync(ct);

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
