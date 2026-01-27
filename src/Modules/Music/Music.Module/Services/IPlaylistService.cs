using SharedKernel.Persistence.ApiModels;

namespace Music.Modules.Services;

public interface IPlaylistService
{
    Task<PlaylistApiModel?> GetPlaylistByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllPlaylistsAsync(CancellationToken ct);
}
