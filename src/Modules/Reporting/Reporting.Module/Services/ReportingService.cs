using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Modules.Data;
using Reporting.Modules.Domain;
using SharedKernel.Concurrency;

namespace Reporting.Modules.Services;

/// <summary>
/// Reads Reporting's views and findings. View queries scan other modules' tables, so each one waits for a
/// slot in Reporting's gate (Concurrency:Reporting:MaxConcurrentExpensive, default 4; ADR-0013).
/// </summary>
internal sealed class ReportingService(
    ReportingDbContext db,
    [FromKeyedServices(ReportingModule.ModuleName)] ModuleGate gate)
{
    public async Task<IReadOnlyList<SalesByGenreRow>> SalesByGenreAsync(CancellationToken ct)
    {
        using var slot = await gate.EnterAsync(ct);
        return await db.SalesByGenre.AsNoTracking()
            .OrderByDescending(row => row.UnitsSold).ThenBy(row => row.GenreName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InvoiceLineWithNamesRow>> InvoiceLinesAsync(int invoiceId, CancellationToken ct)
    {
        using var slot = await gate.EnterAsync(ct);
        return await db.InvoiceLinesWithNames.AsNoTracking()
            .Where(row => row.InvoiceId == invoiceId)
            .OrderBy(row => row.InvoiceLineId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<IntegrityFinding>> OpenFindingsAsync(CancellationToken ct)
    {
        return await db.IntegrityFindings.AsNoTracking()
            .Where(finding => finding.ResolvedAt == null)
            .OrderBy(finding => finding.CheckName).ThenBy(finding => finding.SourceId)
            .ToListAsync(ct);
    }
}
