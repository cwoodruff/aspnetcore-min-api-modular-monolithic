using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;

namespace SharedKernel.Persistence.Repositories;

public interface IPlaylistRepository : IRepository<Playlist>, IDisposable
{
    Task<List<Playlist>> GetByTrackId(int id);
    Task<PlaylistApiModel> GetById(int id);
}
