using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace Reporting.Modules.Endpoints;

internal static class ReportingHealthEndpoints
{
    public static void MapReportingHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/health", ReportingHealthHandlers.Health)
            .WithName("ReportingHealth")
            .Produces(200)
            .WithTags("Reporting")
            .Produces(429); // Rate limited by the module group's policy
    }
}

/// <summary>Operational endpoint handlers. They return <see cref="IResult" />: the body is minimal or detailed
/// depending on the environment (BuildInfoProvider), so there is no single typed shape.</summary>
internal static class ReportingHealthHandlers
{
    public static IResult Health(IHostEnvironment env, IConfiguration cfg)
    {
        var timestampUtc = DateTime.UtcNow.ToString("O");

        if (!BuildInfoProvider.ShouldExposeOperationalMetadata(env))
        {
            return Results.Json(new
            {
                module = "Reporting",
                status = "Healthy",
                timestampUtc
            });
        }

        var response = new
        {
            module = "Reporting",
            status = "Healthy",
            timestampUtc,
            environment = BuildInfoProvider.GetEnvironment(env),
            version = BuildInfoProvider.GetInformationalVersion(typeof(ReportingModule).Assembly),
            service = BuildInfoProvider.GetServiceName(cfg)
        };
        return Results.Json(response);
    }
}
