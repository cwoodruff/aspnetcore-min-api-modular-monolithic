using Admin.Modules.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Admin.Modules;

public static class AdministrationModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Administration";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Register module-specific services here in the future
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/administration");

            // Delegate to endpoint classes
            group.MapAdministrationHealthEndpoints();
            group.MapAdministrationDataHealthEndpoints();
        }
    }
}
