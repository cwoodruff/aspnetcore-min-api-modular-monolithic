using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using SharedKernel.Persistence;

namespace Identity.Modules;

public static class IdentityModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Identity";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Register module-specific services here in the future
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/identity").WithTags(Name);

            group.MapGet("/health", (IHostEnvironment env, IConfiguration cfg) =>
            {
                var response = new
                {
                    module = Name,
                    status = "Healthy",
                    timestampUtc = DateTime.UtcNow.ToString("O"),
                    environment = BuildInfoProvider.GetEnvironment(env),
                    version = BuildInfoProvider.GetInformationalVersion(typeof(Modules).Assembly),
                    service = BuildInfoProvider.GetServiceName(cfg),
                };
                return Results.Json(response);
            })
            .WithName("IdentityHealth")
            .Produces(200);

            group.MapGet("/data-health" , async (AppDbContext db, IHostEnvironment env, IConfiguration cfg, CancellationToken ct) =>
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
                        module = Name,
                        status = canConnect ? "Data-Healthy" : "Degraded",
                        timestampUtc = DateTime.UtcNow.ToString("O"),
                        environment = BuildInfoProvider.GetEnvironment(env),
                        version = BuildInfoProvider.GetInformationalVersion(typeof(Modules).Assembly),
                        service = BuildInfoProvider.GetServiceName(cfg),
                        database = new { connected = canConnect }
                    };
                    return Results.Json(response);
                })
                .WithName("IdentityDataHealth")
                .Produces(200);
        }
    }
}
