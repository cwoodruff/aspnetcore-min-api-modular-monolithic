using Catalog.Modules.Models;

namespace Catalog.Modules.Services;

internal interface ITrackService
{
    Task<TrackApiModel?> GetTrackByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<TrackApiModel>> GetAllTracksAsync(CancellationToken ct);
    Task<IReadOnlyList<TrackApiModel>> GetTracksByArtistIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<TrackApiModel>> GetTracksByPlaylistIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<TrackApiModel>> GetTracksByAlbumIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<TrackApiModel>> GetTracksByGenreIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<TrackApiModel>> GetTracksByMediaTypeIdAsync(int id, CancellationToken ct);
    Task<TrackApiModel?> CreateTrackAsync(TrackApiModel model, CancellationToken ct);
    Task<bool> UpdateTrackAsync(TrackApiModel model, CancellationToken ct);
    Task<TrackSalesApiModel?> GetTrackSalesAsync(int id, CancellationToken ct);
}
