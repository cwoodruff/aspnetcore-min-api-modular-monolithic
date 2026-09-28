using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Catalog.Modules.Services;
using SharedKernel.TrafficControl;

namespace Catalog.Modules.Endpoints;

internal static class ArtistEndpoints
{
    public static void MapArtistEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/catalog/artists/{id}
        group.MapGet("/artists/{id:int}", [Authorize] async (
                int id,
                IArtistService service,
                CancellationToken ct) =>
            {
                var artist = await service.GetArtistByIdAsync(id, ct);

                return artist is not null ? TypedResults.Ok(artist) : Results.NotFound();
            })
            .RequireAuthorization("catalog.read").RequireAuthorization("tenant.scoped")
            .WithName("CatalogGetArtistById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/catalog/artists
        group.MapGet("artists/", [Authorize] async (
                IArtistService service,
                CancellationToken ct) =>
            {
                var artists = await service.GetAllArtistsAsync(ct);

                return Results.Json(artists);
            })
            .RequireAuthorization("catalog.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllArtists")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
