using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace Music.Modules.Endpoints;

public static class MusicHealthEndpoints
{
    public static void MapMusicHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/health", (IHostEnvironment env, IConfiguration cfg) =>
            {
                var response = new
                {
                    module = "Music",
                    status = "Healthy",
                    timestampUtc = DateTime.UtcNow.ToString("O"),
                    environment = BuildInfoProvider.GetEnvironment(env),
                    version = BuildInfoProvider.GetInformationalVersion(typeof(MusicModule).Assembly),
                    service = BuildInfoProvider.GetServiceName(cfg),
                };
                return Results.Json(response);
            })
            .WithName("MusicHealth")
            .Produces(200)
            .WithTags("Music");
    }
}
