using Orders.Modules.Models;

namespace Orders.Modules.Services;

internal interface IInvoiceLineService
{
    Task<InvoiceLineApiModel?> GetInvoiceLineByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<InvoiceLineApiModel>> GetAllInvoiceLinesAsync(CancellationToken ct);
    Task<IReadOnlyList<InvoiceLineApiModel>> GetInvoiceLinesByInvoiceIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<InvoiceLineApiModel>> GetInvoiceLinesByTrackIdAsync(int id, CancellationToken ct);
    Task<InvoiceLineApiModel?> CreateInvoiceLineAsync(InvoiceLineApiModel model, CancellationToken ct);
    Task<bool> UpdateInvoiceLineAsync(InvoiceLineApiModel model, CancellationToken ct);
}
