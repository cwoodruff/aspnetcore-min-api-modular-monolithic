using SharedKernel.Persistence.ApiModels;

namespace Music.Modules.Services;

public interface IAlbumService
{
    Task<AlbumApiModel?> GetAlbumByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllAlbumsAsync(CancellationToken ct);
    Task<IEnumerable<object>> GetAlbumsByArtistIdAsync(int id, CancellationToken ct);
    Task<AlbumApiModel?> CreateAlbumAsync(AlbumApiModel model, CancellationToken ct);
    Task<bool> UpdateAlbumAsync(AlbumApiModel model, CancellationToken ct);
}
