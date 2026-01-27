using SharedKernel.Persistence.ApiModels;

namespace Orders.Modules.Services;

public interface IInvoiceService
{
    Task<object?> GetInvoiceByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllInvoicesAsync(CancellationToken ct);
    Task<IEnumerable<object>> GetInvoicesByCustomerIdAsync(int id, CancellationToken ct);
    Task<InvoiceApiModel?> CreateInvoiceAsync(InvoiceApiModel model, CancellationToken ct);
    Task<bool> UpdateInvoiceAsync(InvoiceApiModel model, CancellationToken ct);
}
