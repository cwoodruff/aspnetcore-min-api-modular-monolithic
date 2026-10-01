using Catalog.Modules.Models;

namespace Catalog.Modules.Services;

internal interface IPlaylistService
{
    Task<PlaylistApiModel?> GetPlaylistByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<PlaylistApiModel>> GetAllPlaylistsAsync(CancellationToken ct);
    Task<PlaylistApiModel?> CreatePlaylistAsync(PlaylistApiModel model, CancellationToken ct);
    Task<bool> UpdatePlaylistAsync(PlaylistApiModel model, CancellationToken ct);
}
