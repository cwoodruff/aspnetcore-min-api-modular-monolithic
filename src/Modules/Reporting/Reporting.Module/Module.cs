using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Modules.Endpoints;
using SharedKernel;
using SharedKernel.Diagnostics;
using SharedKernel.TrafficControl;

namespace Reporting.Modules;

public static class ReportingModule
{
    /// <summary>The module's name and its key for keyed services, caches, metrics and rate limits.</summary>
    internal const string ModuleName = "Reporting";

    public sealed class Modules : IModule
    {
        public string Name => ModuleName;

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            services.AddModuleMeter(ModuleName);
            services.AddModuleRateLimitPolicy(RateLimitPolicyRegistry.Names.Reporting);
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/reporting")
                .RequireRateLimiting(RateLimitPolicyRegistry.Names.Reporting)
                .AddModuleMetrics(ModuleName);

            // Delegate to endpoint classes
            group.MapReportingHealthEndpoints();
            group.MapReportingDataHealthEndpoints();
        }
    }
}
