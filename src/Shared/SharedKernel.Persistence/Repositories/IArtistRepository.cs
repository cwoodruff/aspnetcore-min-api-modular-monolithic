using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.Repositories;

public interface IArtistRepository : IRepository<Artist>, IDisposable
{
    Task<ArtistApiModel> GetById(int id);
}
