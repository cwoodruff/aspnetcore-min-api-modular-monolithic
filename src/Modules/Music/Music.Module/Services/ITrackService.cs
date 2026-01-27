using SharedKernel.Persistence.ApiModels;

namespace Music.Modules.Services;

public interface ITrackService
{
    Task<object?> GetTrackByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllTracksAsync(CancellationToken ct);
    Task<IEnumerable<object>> GetTracksByArtistIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetTracksByPlaylistIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetTracksByAlbumIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetTracksByGenreIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetTracksByMediaTypeIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetTracksByInvoiceIdAsync(int id, CancellationToken ct);
}
