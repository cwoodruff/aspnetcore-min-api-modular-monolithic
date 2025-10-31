using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using SharedKernel.Persistence;

namespace Identity.Modules.Endpoints;

public static class IdentityDataHealthEndpoints
{
    public static void MapIdentityDataHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/data-health", async (AppDbContext db, IHostEnvironment env, IConfiguration cfg, CancellationToken ct) =>
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

                var response = new
                {
                    module = "Identity",
                    status = canConnect ? "Data-Healthy" : "Degraded",
                    timestampUtc = DateTime.UtcNow.ToString("O"),
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
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
