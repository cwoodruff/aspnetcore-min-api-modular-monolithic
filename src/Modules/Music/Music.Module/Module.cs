using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Music.Modules;

public static class MusicModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Music";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Register module-specific services here in the future
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/music");

            // Delegate to endpoint classes
            group.MapMusicHealthEndpoints();
            group.MapMusicDataHealthEndpoints();
        }
    }
}
