using SharedKernel.Persistence.ApiModels;

namespace Music.Modules.Services;

public interface IAlbumService
{
    Task<AlbumApiModel?> GetAlbumByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllAlbumsAsync(CancellationToken ct);
    Task<IEnumerable<object>> GetAlbumsByArtistIdAsync(int id, CancellationToken ct);
}
