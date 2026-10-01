using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using Reporting.Modules.Data;

namespace Reporting.Modules.Endpoints;

internal static class ReportingDataHealthEndpoints
{
    public static void MapReportingDataHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/data-health", ReportingDataHealthHandlers.DataHealth)
            .WithName("ReportingDataHealth")
            .Produces(200)
            .WithTags("Reporting")
            .Produces(429); // Rate limited by the module group's policy
    }
}

/// <summary>Operational endpoint handlers. They return <see cref="IResult" />: the body is minimal or detailed
/// depending on the environment (BuildInfoProvider), so there is no single typed shape.</summary>
internal static class ReportingDataHealthHandlers
{
    public static async Task<IResult> DataHealth(ReportingDbContext db, IHostEnvironment env, IConfiguration cfg, CancellationToken ct)
    {
        // Connects as the read-only reporting role, as the module's queries do.
        bool canConnect;
        try
        {
            canConnect = await db.Database.CanConnectAsync(ct);
        }
        catch
        {
            canConnect = false;
        }

        var timestampUtc = DateTime.UtcNow.ToString("O");
        var status = canConnect ? "Data-Healthy" : "Degraded";

        if (!BuildInfoProvider.ShouldExposeOperationalMetadata(env))
        {
            return Results.Json(new
            {
                module = "Reporting",
                status,
                timestampUtc
            });
        }

        var response = new
        {
            module = "Reporting",
            status,
            timestampUtc,
            environment = BuildInfoProvider.GetEnvironment(env),
            version = BuildInfoProvider.GetInformationalVersion(typeof(ReportingModule).Assembly),
            service = BuildInfoProvider.GetServiceName(cfg),
            database = new { connected = canConnect }
        };
        return Results.Json(response);
    }
}
