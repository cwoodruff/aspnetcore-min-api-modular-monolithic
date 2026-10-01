using Catalog.Modules.Services;
using Identity.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("CatalogGetPlaylistById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/playlists
        group.MapGet("playlists/", [Authorize] async (
                IPlaylistService service,
                CancellationToken ct) =>
            {
                var playlists = await service.GetAllPlaylistsAsync(ct);

                return Results.Json(playlists);
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAllPlaylists")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy
    }
}
