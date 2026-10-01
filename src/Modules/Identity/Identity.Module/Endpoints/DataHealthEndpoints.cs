using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using SharedKernel.Persistence;

namespace Identity.Modules.Endpoints;

internal static class IdentityDataHealthEndpoints
{
    public static void MapIdentityDataHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/data-health", IdentityDataHealthHandlers.DataHealth)
            .WithName("IdentityDataHealth")
            .Produces(200)
            .WithTags("Identity")
            .Produces(429); // Rate limited by the module group's policy
    }
}

/// <summary>Operational endpoint handlers. They return <see cref="IResult" />: the body is minimal or detailed
/// depending on the environment (BuildInfoProvider), so there is no single typed shape.</summary>
internal static class IdentityDataHealthHandlers
{
    public static async Task<IResult> DataHealth(IHostEnvironment env, IConfiguration cfg, CancellationToken ct)
    {
        // No tables of its own yet; report whether the shared database is reachable.
        var canConnect = await ModuleDbContextOptions.CanConnectAsync(cfg, ct);

        var timestampUtc = DateTime.UtcNow.ToString("O");
        var status = canConnect ? "Data-Healthy" : "Degraded";

        if (!BuildInfoProvider.ShouldExposeOperationalMetadata(env))
        {
            return Results.Json(new
            {
                module = "Identity",
                status,
                timestampUtc
            });
        }

        var response = new
        {
            module = "Identity",
            status,
            timestampUtc,
            environment = BuildInfoProvider.GetEnvironment(env),
            version = BuildInfoProvider.GetInformationalVersion(typeof(IdentityModule).Assembly),
            service = BuildInfoProvider.GetServiceName(cfg),
            database = new { connected = canConnect }
        };
        return Results.Json(response);
    }
}
