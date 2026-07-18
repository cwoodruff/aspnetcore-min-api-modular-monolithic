using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using SharedKernel.TrafficControl;

namespace Admin.Modules.Endpoints;

internal static class AdministrationHealthEndpoints
{
    public static void MapAdministrationHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/health", (IHostEnvironment env, IConfiguration cfg) =>
            {
                var response = new
                {
                    module = "Administration",
                    status = "Healthy",
                    timestampUtc = DateTime.UtcNow.ToString("O"),
                    environment = BuildInfoProvider.GetEnvironment(env),
                    version = BuildInfoProvider.GetInformationalVersion(typeof(AdministrationModule).Assembly),
                    service = BuildInfoProvider.GetServiceName(cfg)
                };
                return Results.Json(response);
            })
            .WithName("AdministrationHealth")
            .Produces(200)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
