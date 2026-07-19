using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using SharedKernel.Persistence;
using SharedKernel.TrafficControl;

namespace Identity.Modules.Endpoints;

internal static class IdentityDataHealthEndpoints
{
    public static void MapIdentityDataHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/data-health",
                async (AppDbContext db, IHostEnvironment env, IConfiguration cfg, CancellationToken ct) =>
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
                })
            .WithName("IdentityDataHealth")
            .Produces(200)
            .WithTags("Identity")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
