using Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Reporting.Modules.Integrity;
using Reporting.Modules.Services;

namespace Reporting.Modules.Endpoints;

internal static class ReportingEndpoints
{
    public static void MapReportingEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/reporting/sales-by-genre
        group.MapGet("/sales-by-genre", ReportingHandlers.SalesByGenre)
            .RequireAuthorization(Permissions.ReportView).RequireAuthorization(Policies.TenantScoped)
            .WithName("ReportingSalesByGenre")
            .WithDescription("Units sold per genre, from Catalog's TrackSales (eventually consistent, ADR-0009).")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Reporting")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/reporting/invoices/{id}/lines
        group.MapGet("/invoices/{id:int}/lines", ReportingHandlers.InvoiceLines)
            .RequireAuthorization(Permissions.ReportView).RequireAuthorization(Policies.TenantScoped)
            .WithName("ReportingInvoiceLines")
            .WithDescription("An invoice's lines with track and customer names, read across modules through a view.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Reporting")
            .Produces(429); // Rate limited by the module group's policy

        // POST /api/reporting/integrity/run
        group.MapPost("/integrity/run", ReportingHandlers.RunIntegrityCheck)
            .RequireAuthorization(Policies.Admin)
            .WithName("ReportingRunIntegrityCheck")
            .WithDescription("Runs the orphan checks now and returns how many findings were added, resolved and left open.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Reporting")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/reporting/integrity/findings
        group.MapGet("/integrity/findings", ReportingHandlers.IntegrityFindings)
            .RequireAuthorization(Policies.Admin)
            .WithName("ReportingIntegrityFindings")
            .WithDescription("Open findings: rows whose cross-module reference points at nothing. Nothing is repaired.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Reporting")
            .Produces(429); // Rate limited by the module group's policy
    }
}
