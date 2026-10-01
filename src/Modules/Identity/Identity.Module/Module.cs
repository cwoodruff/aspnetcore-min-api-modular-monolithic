using Identity.Modules.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using SharedKernel.Diagnostics;
using SharedKernel.TrafficControl;

namespace Identity.Modules;

public static class IdentityModule
{
    /// <summary>The module's name and its key for keyed services, caches, metrics and rate limits.</summary>
    internal const string ModuleName = "Identity";

    public sealed class Modules : IModule
    {
        public string Name => ModuleName;

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            services.AddModuleMeter(ModuleName);
            services.AddModuleRateLimitPolicy(RateLimitPolicyRegistry.Names.Identity);
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/identity")
                .RequireRateLimiting(RateLimitPolicyRegistry.Names.Identity)
                .AddModuleMetrics(ModuleName);

            // Delegate to endpoint classes
            group.MapIdentityHealthEndpoints();
            group.MapIdentityDataHealthEndpoints();
            group.MapIdentityAuthEndpoints();
        }
    }
}
