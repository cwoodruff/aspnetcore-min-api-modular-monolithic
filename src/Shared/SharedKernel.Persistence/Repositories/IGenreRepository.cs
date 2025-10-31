using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.Repositories;

public interface IGenreRepository : IRepository<Genre>, IDisposable
{
    Task<Genre> GetById(int id);
}
