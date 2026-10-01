using Catalog.Modules.Services;
using Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Modules.Endpoints;

internal static class ArtistEndpoints
{
    public static void MapArtistEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/catalog/artists/{id}
        group.MapGet("/artists/{id:int}", ArtistHandlers.GetArtistById)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("CatalogGetArtistById")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/artists
        group.MapGet("artists/", ArtistHandlers.GetAllArtists)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAllArtists")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy
    }
}
