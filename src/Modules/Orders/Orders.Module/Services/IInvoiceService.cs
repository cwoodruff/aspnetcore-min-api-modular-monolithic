using Orders.Modules.Models;

namespace Orders.Modules.Services;

internal interface IInvoiceService
{
    Task<object?> GetInvoiceByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllInvoicesAsync(CancellationToken ct);
    Task<IEnumerable<object>> GetInvoicesByCustomerIdAsync(int id, CancellationToken ct);
    Task<InvoiceApiModel?> CreateInvoiceAsync(InvoiceApiModel model, CancellationToken ct);
    Task<bool> UpdateInvoiceAsync(InvoiceApiModel model, CancellationToken ct);
    Task<FinalizeInvoiceResult> FinalizeInvoiceAsync(int id, CancellationToken ct);
}

internal enum FinalizeInvoiceResult
{
    Finalized,
    NotFound,
    AlreadyFinalized
}
