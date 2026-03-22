using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.Repositories;

public interface ICustomerRepository : IRepository<Customer>, IDisposable
{
    Task<List<Customer>> GetBySupportRepId(int id);

    Task<CustomerApiModel?> GetById(int id);
}
