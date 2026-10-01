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
using SharedKernel.Caching;
using SharedKernel.Diagnostics;
using SharedKernel.Events;
using SharedKernel.Persistence;
using SharedKernel.TrafficControl;

namespace Catalog.Modules;

public static class CatalogModule
{
    /// <summary>The module's name and its key for keyed services, caches, metrics and rate limits.</summary>
    internal const string ModuleName = "Catalog";

    public sealed class Modules : IModule
    {
        public string Name => ModuleName;

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            services.AddModuleDbContext<CatalogDbContext>(ModuleName, CatalogDbContext.Schema);
            services.AddModuleCache(ModuleName, config.GetValue($"Caching:Modules:{ModuleName}:SizeLimit", 1000));
            services.AddModuleRateLimitPolicy(RateLimitPolicyRegistry.Catalog);
            services.AddValidatorsFromAssemblyContaining<AlbumValidator>(includeInternalTypes: true);

            services.AddScoped<IAlbumService, AlbumService>();
            services.AddScoped<IArtistService, ArtistService>();
            services.AddScoped<IPlaylistService, PlaylistService>();
            services.AddScoped<ITrackService, TrackService>();

            services.AddIntegrationEventHandler<InvoiceFinalized, InvoiceFinalizedHandler>(ModuleName);
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/catalog")
                .RequireRateLimiting(RateLimitPolicyRegistry.Catalog)
                .AddModuleMetrics(ModuleName);

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
