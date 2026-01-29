using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using SharedKernel.TrafficControl;

namespace Reporting.Modules.Endpoints;

public static class ReportingHealthEndpoints
{
    public static void MapReportingHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/health", (IHostEnvironment env, IConfiguration cfg) =>
            {
                var response = new
                {
                    module = "Reporting",
                    status = "Healthy",
                    timestampUtc = DateTime.UtcNow.ToString("O"),
                    environment = BuildInfoProvider.GetEnvironment(env),
                    version = BuildInfoProvider.GetInformationalVersion(typeof(ReportingModule).Assembly),
                    service = BuildInfoProvider.GetServiceName(cfg)
                };
                return Results.Json(response);
            })
            .WithName("ReportingHealth")
            .Produces(200)
            .WithTags("Reporting")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
