using Catalog.Modules.Services;
using Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Modules.Endpoints;

internal static class TrackEndpoints
{
    public static void MapTrackEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/catalog/tracks/{id}
        group.MapGet("/tracks/{id:int}", TrackHandlers.GetTrackById)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("CatalogGetTrackById")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/{id}/sales
        group.MapGet("/tracks/{id:int}/sales", TrackHandlers.GetTrackSales)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("CatalogGetTrackSales")
            .WithDescription(
                "Units sold, counted from InvoiceFinalized events. Eventually consistent: an invoice finalized " +
                "moments ago may not be counted yet.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks
        group.MapGet("tracks/", TrackHandlers.GetAllTracks)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAllTracks")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/artist/{id}
        group.MapGet("tracks/artist/{id:int}", TrackHandlers.GetTracksByArtistId)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetTracksByArtistId")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/playlist/{id}
        group.MapGet("tracks/playlist/{id:int}", TrackHandlers.GetTracksByPlaylistId)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetTracksByPlaylistId")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/album/{id}
        group.MapGet("tracks/album/{id:int}", TrackHandlers.GetTracksByAlbumId)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetTracksByAlbumId")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/genre/{id}
        group.MapGet("tracks/genre/{id:int}", TrackHandlers.GetTracksByGenreId)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetTracksByGenreId")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/mediatype/{id}
        group.MapGet("tracks/mediatype/{id:int}", TrackHandlers.GetTracksByMediaTypeId)
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetTracksByMediaTypeId")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy
    }
}
