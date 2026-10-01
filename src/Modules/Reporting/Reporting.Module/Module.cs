using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reporting.Modules.Data;
using Reporting.Modules.Endpoints;
using Reporting.Modules.Integrity;
using Reporting.Modules.Services;
using SharedKernel;
using SharedKernel.Diagnostics;
using SharedKernel.Persistence;
using SharedKernel.TrafficControl;

namespace Reporting.Modules;

public static class ReportingModule
{
    /// <summary>The module's name and its key for keyed services, caches, metrics and rate limits.</summary>
    internal const string ModuleName = "Reporting";

    /// <summary>ConnectionStrings:Reporting, a login in the reporting_reader role.</summary>
    internal const string ReportingConnectionName = "Reporting";

    public sealed class Modules : IModule
    {
        public string Name => ModuleName;

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Runtime queries use the read-only role; migrations run as the schema owner (ADR-0014).
            services.AddModuleDbContext<ReportingDbContext>(ModuleName, ReportingDbContext.Schema,
                connectionName: ReportingConnectionName,
                migrationConnectionName: ModuleDbContextOptions.ConnectionName);
            services.AddModuleMeter(ModuleName);
            services.AddModuleRateLimitPolicy(RateLimitPolicyRegistry.Names.Reporting);
            services.AddModuleGate(ModuleName);
            services.AddModuleWorkQueue(ModuleName);

            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<IntegrityCheckJob>();
            services.AddHostedService(sp => sp.GetRequiredService<IntegrityCheckJob>());
            services.AddScoped<ReportingService>();
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/reporting")
                .RequireRateLimiting(RateLimitPolicyRegistry.Names.Reporting)
                .AddModuleMetrics(ModuleName);

            // Delegate to endpoint classes
            group.MapReportingHealthEndpoints();
            group.MapReportingDataHealthEndpoints();
            group.MapReportingEndpoints();
        }
    }
}
