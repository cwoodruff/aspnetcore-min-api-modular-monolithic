using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;

namespace SharedKernel.Persistence.Repositories;

public interface IInvoiceLineRepository : IRepository<InvoiceLine>, IDisposable
{
    Task<List<InvoiceLine>> GetByInvoiceId(int id);
    Task<List<InvoiceLine>> GetByTrackId(int id);
    Task<InvoiceLine> GetById(int id);
}
