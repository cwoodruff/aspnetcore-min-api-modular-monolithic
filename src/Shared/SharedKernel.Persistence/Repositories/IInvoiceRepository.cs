using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;

namespace SharedKernel.Persistence.Repositories;

public interface IInvoiceRepository : IRepository<Invoice>, IDisposable
{
    Task<List<Invoice>> GetByCustomerId(int id);
    Task<InvoiceApiModel> GetById(int id);
}
