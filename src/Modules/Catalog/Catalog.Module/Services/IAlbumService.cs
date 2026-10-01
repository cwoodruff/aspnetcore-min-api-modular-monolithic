using Catalog.Modules.Models;

namespace Catalog.Modules.Services;

internal interface IAlbumService
{
    Task<AlbumApiModel?> GetAlbumByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<AlbumApiModel>> GetAllAlbumsAsync(CancellationToken ct);
    Task<IReadOnlyList<AlbumApiModel>> GetAlbumsByArtistIdAsync(int id, CancellationToken ct);
    Task<AlbumApiModel?> CreateAlbumAsync(AlbumApiModel model, CancellationToken ct);
    Task<bool> UpdateAlbumAsync(AlbumApiModel model, CancellationToken ct);
}
