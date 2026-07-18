using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using SharedKernel.TrafficControl;

namespace Identity.Modules.Endpoints;

internal static class IdentityHealthEndpoints
{
    public static void MapIdentityHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/health", (IHostEnvironment env, IConfiguration cfg) =>
            {
                var response = new
                {
                    module = "Identity",
                    status = "Healthy",
                    timestampUtc = DateTime.UtcNow.ToString("O"),
                    environment = BuildInfoProvider.GetEnvironment(env),
                    version = BuildInfoProvider.GetInformationalVersion(typeof(IdentityModule).Assembly),
                    service = BuildInfoProvider.GetServiceName(cfg)
                };
                return Results.Json(response);
            })
            .WithName("IdentityHealth")
            .Produces(200)
            .WithTags("Identity")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
