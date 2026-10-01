using Catalog.Modules.Domain;
using Catalog.Modules.Models;
using Catalog.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Modules.Endpoints;

/// <summary>The track endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class TrackHandlers
{
    [Authorize]
    public static async Task<Results<Ok<TrackApiModel>, NotFound>> GetTrackById(int id, ITrackService service, CancellationToken ct)
    {
        var track = await service.GetTrackByIdAsync(id, ct);
        return track is not null ? TypedResults.Ok(track) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Results<Ok<TrackSalesApiModel>, NotFound>> GetTrackSales(int id, ITrackService service, CancellationToken ct)
    {
        var sales = await service.GetTrackSalesAsync(id, ct);
        return sales is not null ? TypedResults.Ok(sales) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<TrackApiModel>>> GetAllTracks(ITrackService service, CancellationToken ct)
    {
        var tracks = await service.GetAllTracksAsync(ct);
        return TypedResults.Ok(tracks);
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<TrackApiModel>>> GetTracksByArtistId(int id, ITrackService service, CancellationToken ct)
    {
        var tracks = await service.GetTracksByArtistIdAsync(id, ct);
        return TypedResults.Ok(tracks);
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<TrackApiModel>>> GetTracksByPlaylistId(int id, ITrackService service, CancellationToken ct)
    {
        var tracks = await service.GetTracksByPlaylistIdAsync(id, ct);
        return TypedResults.Ok(tracks);
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<TrackApiModel>>> GetTracksByAlbumId(int id, ITrackService service, CancellationToken ct)
    {
        var tracks = await service.GetTracksByAlbumIdAsync(id, ct);
        return TypedResults.Ok(tracks);
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<TrackApiModel>>> GetTracksByGenreId(int id, ITrackService service, CancellationToken ct)
    {
        var tracks = await service.GetTracksByGenreIdAsync(id, ct);
        return TypedResults.Ok(tracks);
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<TrackApiModel>>> GetTracksByMediaTypeId(int id, ITrackService service, CancellationToken ct)
    {
        var tracks = await service.GetTracksByMediaTypeIdAsync(id, ct);
        return TypedResults.Ok(tracks);
    }
}
