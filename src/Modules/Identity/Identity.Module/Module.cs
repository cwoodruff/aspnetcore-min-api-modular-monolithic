using Identity.Modules.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

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
            var group = endpoints.MapGroup("/api/identity");

            // Delegate to endpoint classes
            group.MapIdentityHealthEndpoints();
            group.MapIdentityDataHealthEndpoints();
            group.MapIdentityAuthEndpoints();
        }
    }
}
