using Catalog.Modules.Services;
using Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Modules.Endpoints;

internal static class PlaylistEndpoints
{
    public static void MapPlaylistEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/catalog/playlists/{id}
        group.MapGet("/playlists/{id:int}", PlaylistHandlers.GetPlaylistById)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("CatalogGetPlaylistById")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/playlists
        group.MapGet("playlists/", PlaylistHandlers.GetAllPlaylists)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAllPlaylists")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy
    }
}
