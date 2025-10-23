using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Modules.Endpoints;
using SharedKernel;

namespace Reporting.Modules;

public static class ReportingModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Reporting";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Register module-specific services here in the future
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/reporting");

            // Delegate to endpoint classes
            group.MapReportingHealthEndpoints();
            group.MapReportingDataHealthEndpoints();
        }
    }
}
