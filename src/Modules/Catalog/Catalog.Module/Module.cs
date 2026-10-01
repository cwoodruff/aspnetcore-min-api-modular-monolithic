using Catalog.Modules.Data;
using Catalog.Modules.Endpoints;
using Catalog.Modules.Events;
using Catalog.Modules.Services;
using Catalog.Modules.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Contracts.Events;
using SharedKernel;
using SharedKernel.Events;
using SharedKernel.Persistence;

namespace Catalog.Modules;

public static class CatalogModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Catalog";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            services.AddModuleDbContext<CatalogDbContext>(CatalogDbContext.Schema);
            services.AddValidatorsFromAssemblyContaining<AlbumValidator>(includeInternalTypes: true);

            services.AddScoped<IAlbumService, AlbumService>();
            services.AddScoped<IArtistService, ArtistService>();
            services.AddScoped<IPlaylistService, PlaylistService>();
            services.AddScoped<ITrackService, TrackService>();

            services.AddScoped<IIntegrationEventHandler<InvoiceFinalized>, InvoiceFinalizedHandler>();
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/catalog");

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
