using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.Repositories;

public interface IPlaylistRepository : IRepository<Playlist>, IDisposable
{
    Task<List<Playlist>> GetByTrackId(int id);
    Task<PlaylistApiModel> GetById(int id);
}
