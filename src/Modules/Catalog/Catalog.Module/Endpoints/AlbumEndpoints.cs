using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Catalog.Modules.Services;
using SharedKernel.TrafficControl;

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
            .RequireAuthorization("catalog.read").RequireAuthorization("tenant.scoped")
            .WithName("CatalogGetAlbumById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/catalog/albums
        group.MapGet("albums/", [Authorize] async (
                IAlbumService service,
                CancellationToken ct) =>
            {
                var albums = await service.GetAllAlbumsAsync(ct);

                return Results.Json(albums);
            })
            .RequireAuthorization("catalog.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllAlbums")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        //
        group.MapGet("albums/artist/{id:int}", [Authorize] async (
                int id,
                IAlbumService service,
                CancellationToken ct) =>
            {
                var albums = await service.GetAlbumsByArtistIdAsync(id, ct);

                return Results.Json(albums);
            })
            .RequireAuthorization("catalog.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAlbumsByArtistId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
