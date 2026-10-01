using Catalog.Modules.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace Catalog.Modules.Endpoints;

internal static class CatalogDataHealthEndpoints
{
    public static void MapCatalogDataHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/data-health", CatalogDataHealthHandlers.DataHealth)
            .WithName("CatalogDataHealth")
            .Produces(200)
            .WithTags("Catalog")
            .Produces(429); // Rate limited by the module group's policy
    }
}

/// <summary>Operational endpoint handlers. They return <see cref="IResult" />: the body is minimal or detailed
/// depending on the environment (BuildInfoProvider), so there is no single typed shape.</summary>
internal static class CatalogDataHealthHandlers
{
    public static async Task<IResult> DataHealth(CatalogDbContext db, IHostEnvironment env, IConfiguration cfg, CancellationToken ct)
    {
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
                module = "Catalog",
                status,
                timestampUtc
            });
        }

        var response = new
        {
            module = "Catalog",
            status,
            timestampUtc,
            environment = BuildInfoProvider.GetEnvironment(env),
            version = BuildInfoProvider.GetInformationalVersion(typeof(CatalogModule).Assembly),
            service = BuildInfoProvider.GetServiceName(cfg),
            database = new { connected = canConnect }
        };
        return Results.Json(response);
    }
}
