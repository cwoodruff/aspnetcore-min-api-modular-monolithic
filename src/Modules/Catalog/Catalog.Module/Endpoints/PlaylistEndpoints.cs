using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Catalog.Modules.Services;
using SharedKernel.TrafficControl;

namespace Catalog.Modules.Endpoints;

internal static class PlaylistEndpoints
{
    public static void MapPlaylistEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/catalog/playlists/{id}
        group.MapGet("/playlists/{id:int}", [Authorize] async (
                int id,
                IPlaylistService service,
                CancellationToken ct) =>
            {
                var playlist = await service.GetPlaylistByIdAsync(id, ct);

                return playlist is not null ? TypedResults.Ok(playlist) : Results.NotFound();
            })
            .RequireAuthorization("catalog.read").RequireAuthorization("tenant.scoped")
            .WithName("CatalogGetPlaylistById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/catalog/playlists
        group.MapGet("playlists/", [Authorize] async (
                IPlaylistService service,
                CancellationToken ct) =>
            {
                var playlists = await service.GetAllPlaylistsAsync(ct);

                return Results.Json(playlists);
            })
            .RequireAuthorization("catalog.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllPlaylists")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
