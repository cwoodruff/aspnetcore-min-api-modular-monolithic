using Catalog.Modules.Services;
using Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Modules.Endpoints;

internal static class AlbumEndpoints
{
    public static void MapAlbumEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/catalog/albums/{id}
        group.MapGet("/albums/{id:int}", AlbumHandlers.GetAlbumById)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("CatalogGetAlbumById")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/albums
        group.MapGet("albums/", AlbumHandlers.GetAllAlbums)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAllAlbums")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        //
        group.MapGet("albums/artist/{id:int}", AlbumHandlers.GetAlbumsByArtistId)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAlbumsByArtistId")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy
    }
}
