using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace Catalog.Modules.Endpoints;

internal static class CatalogHealthEndpoints
{
    public static void MapCatalogHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/health", CatalogHealthHandlers.Health)
            .WithName("CatalogHealth")
            .Produces(200)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy
    }
}

/// <summary>Operational endpoint handlers. They return <see cref="IResult" />: the body is minimal or detailed
/// depending on the environment (BuildInfoProvider), so there is no single typed shape.</summary>
internal static class CatalogHealthHandlers
{
    public static IResult Health(IHostEnvironment env, IConfiguration cfg)
    {
        var timestampUtc = DateTime.UtcNow.ToString("O");

        if (!BuildInfoProvider.ShouldExposeOperationalMetadata(env))
        {
            return Results.Json(new
            {
                module = "Catalog",
                status = "Healthy",
                timestampUtc
            });
        }

        var response = new
        {
            module = "Catalog",
            status = "Healthy",
            timestampUtc,
            environment = BuildInfoProvider.GetEnvironment(env),
            version = BuildInfoProvider.GetInformationalVersion(typeof(CatalogModule).Assembly),
            service = BuildInfoProvider.GetServiceName(cfg)
        };
        return Results.Json(response);
    }
}
