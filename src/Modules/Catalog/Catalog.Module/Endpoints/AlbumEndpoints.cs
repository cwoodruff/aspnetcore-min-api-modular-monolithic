using Catalog.Modules.Services;
using Identity.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Modules.Endpoints;

internal static class AlbumEndpoints
{
    public static void MapAlbumEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/catalog/albums/{id}
        group.MapGet("/albums/{id:int}", [Authorize] async (
                int id,
                IAlbumService service,
                CancellationToken ct) =>
            {
                var album = await service.GetAlbumByIdAsync(id, ct);

                return album is not null ? TypedResults.Ok(album) : Results.NotFound();
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("CatalogGetAlbumById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/albums
        group.MapGet("albums/", [Authorize] async (
                IAlbumService service,
                CancellationToken ct) =>
            {
                var albums = await service.GetAllAlbumsAsync(ct);

                return Results.Json(albums);
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAllAlbums")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        //
        group.MapGet("albums/artist/{id:int}", [Authorize] async (
                int id,
                IAlbumService service,
                CancellationToken ct) =>
            {
                var albums = await service.GetAlbumsByArtistIdAsync(id, ct);

                return Results.Json(albums);
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAlbumsByArtistId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy
    }
}
