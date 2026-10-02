using Reporting.Modules.Domain;

namespace Reporting.Modules.Services;

internal interface IReportingService
{
    Task<IReadOnlyList<SalesByGenreRow>> SalesByGenreAsync(CancellationToken ct);
    Task<IReadOnlyList<InvoiceLineWithNamesRow>> InvoiceLinesAsync(int invoiceId, CancellationToken ct);
    Task<IReadOnlyList<IntegrityFinding>> OpenFindingsAsync(CancellationToken ct);
}
