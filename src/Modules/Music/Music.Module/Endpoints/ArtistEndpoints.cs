using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Music.Modules.Services;
using SharedKernel.TrafficControl;

namespace Music.Modules.Endpoints;

public static class ArtistEndpoints
{
    public static void MapArtistEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/music/artists/{id}
        group.MapGet("/artists/{id:int}", [Authorize] async (
                int id,
                IArtistService service,
                CancellationToken ct) =>
            {
                var artist = await service.GetArtistByIdAsync(id, ct);

                return artist is not null ? TypedResults.Ok(artist) : Results.NotFound();
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("MusicGetArtistById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/artists
        group.MapGet("artists/", [Authorize] async (
                IArtistService service,
                CancellationToken ct) =>
            {
                var artists = await service.GetAllArtistsAsync(ct);

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
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
