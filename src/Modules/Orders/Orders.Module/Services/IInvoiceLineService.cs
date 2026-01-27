using SharedKernel.Persistence.ApiModels;

namespace Orders.Modules.Services;

public interface IInvoiceLineService
{
    Task<object?> GetInvoiceLineByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllInvoiceLinesAsync(CancellationToken ct);
    Task<IEnumerable<object>> GetInvoiceLinesByInvoiceIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetInvoiceLinesByTrackIdAsync(int id, CancellationToken ct);
    Task<InvoiceLineApiModel?> CreateInvoiceLineAsync(InvoiceLineApiModel model, CancellationToken ct);
    Task<bool> UpdateInvoiceLineAsync(InvoiceLineApiModel model, CancellationToken ct);
}
