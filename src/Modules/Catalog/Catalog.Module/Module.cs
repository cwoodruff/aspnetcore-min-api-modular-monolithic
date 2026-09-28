using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Catalog.Modules.Endpoints;
using Catalog.Modules.Services;
using SharedKernel;

namespace Catalog.Modules;

public static class CatalogModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Catalog";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Register module-specific services
            services.AddScoped<IAlbumService, AlbumService>();
            services.AddScoped<IArtistService, ArtistService>();
            services.AddScoped<IPlaylistService, PlaylistService>();
            services.AddScoped<ITrackService, TrackService>();
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/music");

            // Delegate to endpoint classes
            group.MapCatalogHealthEndpoints();
            group.MapCatalogDataHealthEndpoints();
            group.MapAlbumEndpoints();
            group.MapArtistEndpoints();
            group.MapTrackEndpoints();
            group.MapPlaylistEndpoints();
        }
    }
}
