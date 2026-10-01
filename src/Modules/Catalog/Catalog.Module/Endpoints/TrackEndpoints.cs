using Catalog.Modules.Services;
using Identity.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Modules.Endpoints;

internal static class TrackEndpoints
{
    public static void MapTrackEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/catalog/tracks/{id}
        group.MapGet("/tracks/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var track = await service.GetTrackByIdAsync(id, ct);

                return track is not null ? Results.Json(track) : Results.NotFound();
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("CatalogGetTrackById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/{id}/sales
        group.MapGet("/tracks/{id:int}/sales", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var sales = await service.GetTrackSalesAsync(id, ct);

                return sales is not null ? Results.Json(sales) : Results.NotFound();
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("CatalogGetTrackSales")
            .WithDescription(
                "Units sold, counted from InvoiceFinalized events. Eventually consistent: an invoice finalized " +
                "moments ago may not be counted yet.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks
        group.MapGet("tracks/", [Authorize] async (
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetAllTracksAsync(ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAllTracks")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/artist/{id}
        group.MapGet("tracks/artist/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByArtistIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetTracksByArtistId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/playlist/{id}
        group.MapGet("tracks/playlist/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByPlaylistIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetTracksByPlaylistId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/album/{id}
        group.MapGet("tracks/album/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByAlbumIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetTracksByAlbumId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/genre/{id}
        group.MapGet("tracks/genre/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByGenreIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetTracksByGenreId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/catalog/tracks/mediatype/{id}
        group.MapGet("tracks/mediatype/{id:int}", [Authorize] async (
                int id,
                ITrackService service,
                CancellationToken ct) =>
            {
                var tracks = await service.GetTracksByMediaTypeIdAsync(id, ct);

                return Results.Json(tracks);
            })
            .RequireAuthorization(Permissions.CatalogRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetTracksByMediaTypeId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy
    }
}
