using SharedKernel.Persistence.ApiModels;

namespace Music.Modules.Services;

internal interface IPlaylistService
{
    Task<PlaylistApiModel?> GetPlaylistByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllPlaylistsAsync(CancellationToken ct);
    Task<PlaylistApiModel?> CreatePlaylistAsync(PlaylistApiModel model, CancellationToken ct);
    Task<bool> UpdatePlaylistAsync(PlaylistApiModel model, CancellationToken ct);
}
