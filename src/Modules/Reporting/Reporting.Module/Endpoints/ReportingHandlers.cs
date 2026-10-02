using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Reporting.Modules.Domain;
using Reporting.Modules.Integrity;
using Reporting.Modules.Services;

namespace Reporting.Modules.Endpoints;

/// <summary>The reporting endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class ReportingHandlers
{
    [Authorize]
    public static async Task<Ok<IReadOnlyList<SalesByGenreRow>>> SalesByGenre(IReportingService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.SalesByGenreAsync(ct));
    }

    [Authorize]
    public static async Task<Ok<IntegrityRunResult>> RunIntegrityCheck(IntegrityCheckJob job, CancellationToken ct)
    {
        return TypedResults.Ok(await job.RunNowAsync(ct));
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<IntegrityFinding>>> IntegrityFindings(IReportingService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.OpenFindingsAsync(ct));
    }

    [Authorize]
    public static async Task<Results<Ok<IReadOnlyList<InvoiceLineWithNamesRow>>, NotFound>> InvoiceLines(
        int id, IReportingService service, CancellationToken ct)
    {
        var lines = await service.InvoiceLinesAsync(id, ct);
        return lines.Count > 0 ? TypedResults.Ok(lines) : TypedResults.NotFound();
    }
}
