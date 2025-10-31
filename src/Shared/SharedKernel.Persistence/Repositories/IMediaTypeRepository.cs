using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.Repositories;

public interface IMediaTypeRepository : IRepository<MediaType>, IDisposable
{
    Task<MediaType> GetById(int id);
}
