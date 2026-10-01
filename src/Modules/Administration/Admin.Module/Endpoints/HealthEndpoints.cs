using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace Admin.Modules.Endpoints;

internal static class AdministrationHealthEndpoints
{
    public static void MapAdministrationHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/health", AdministrationHealthHandlers.Health)
            .WithName("AdministrationHealth")
            .Produces(200)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy
    }
}

/// <summary>Operational endpoint handlers. They return <see cref="IResult" />: the body is minimal or detailed
/// depending on the environment (BuildInfoProvider), so there is no single typed shape.</summary>
internal static class AdministrationHealthHandlers
{
    public static IResult Health(IHostEnvironment env, IConfiguration cfg)
    {
        var timestampUtc = DateTime.UtcNow.ToString("O");

        if (!BuildInfoProvider.ShouldExposeOperationalMetadata(env))
        {
            return Results.Json(new
            {
                module = "Administration",
                status = "Healthy",
                timestampUtc
            });
        }

        var response = new
        {
            module = "Administration",
            status = "Healthy",
            timestampUtc,
            environment = BuildInfoProvider.GetEnvironment(env),
            version = BuildInfoProvider.GetInformationalVersion(typeof(AdministrationModule).Assembly),
            service = BuildInfoProvider.GetServiceName(cfg)
        };
        return Results.Json(response);
    }
}
