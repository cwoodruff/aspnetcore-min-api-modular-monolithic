using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;

namespace SharedKernel.Persistence.Repositories;

public interface IAlbumRepository : IRepository<Album>, IDisposable
{
    Task<List<Album>> GetByArtistId(int id);
    Task<AlbumApiModel> GetById(int id);
}
