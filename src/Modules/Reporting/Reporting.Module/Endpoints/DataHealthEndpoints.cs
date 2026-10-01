using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using SharedKernel.Persistence;

namespace Reporting.Modules.Endpoints;

internal static class ReportingDataHealthEndpoints
{
    public static void MapReportingDataHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/data-health",
                async (IHostEnvironment env, IConfiguration cfg, CancellationToken ct) =>
                {
                    // No tables of its own yet; report whether the shared database is reachable.
                    var canConnect = await ModuleDbContextOptions.CanConnectAsync(cfg, ct);

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
                })
            .WithName("ReportingDataHealth")
            .Produces(200)
            .WithTags("Reporting")
            .Produces(429); // Rate limited by the module group's policy
    }
}
