using Orders.Modules.Models;

namespace Orders.Modules.Services;

internal interface IInvoiceService
{
    Task<InvoiceApiModel?> GetInvoiceByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<InvoiceApiModel>> GetAllInvoicesAsync(CancellationToken ct);
    Task<IReadOnlyList<InvoiceApiModel>> GetInvoicesByCustomerIdAsync(int id, CancellationToken ct);
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
