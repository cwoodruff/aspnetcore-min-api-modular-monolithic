using Admin.Modules.Data;
using Admin.Modules.Endpoints;
using Admin.Modules.Events;
using Admin.Modules.Services;
using Admin.Modules.Validation;
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

namespace Admin.Modules;

public static class AdministrationModule
{
    /// <summary>The module's name and its key for keyed services, caches, metrics and rate limits.</summary>
    internal const string ModuleName = "Administration";

    public sealed class Modules : IModule
    {
        public string Name => ModuleName;

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            services.AddModuleDbContext<AdministrationDbContext>(ModuleName, AdministrationDbContext.Schema);
            services.AddModuleCache(ModuleName, config.GetValue($"Caching:Modules:{ModuleName}:SizeLimit", 500));
            services.AddModuleRateLimitPolicy(RateLimitPolicyRegistry.Administration);
            services.AddValidatorsFromAssemblyContaining<CustomerValidator>(includeInternalTypes: true);

            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IGenreService, GenreService>();
            services.AddScoped<IEmployeeService, EmployeeService>();
            services.AddScoped<IMediaTypeService, MediaTypeService>();

            services.AddIntegrationEventHandler<InvoiceFinalized, InvoiceFinalizedHandler>(ModuleName);
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/admin")
                .RequireRateLimiting(RateLimitPolicyRegistry.Administration)
                .AddModuleMetrics(ModuleName);

            // Delegate to endpoint classes
            group.MapAdministrationHealthEndpoints();
            group.MapAdministrationDataHealthEndpoints();
            group.MapCustomerEndpoints();
            group.MapEmployeeEndpoints();
            group.MapGenreEndpoints();
            group.MapMediaTypeEndpoints();
        }
    }
}
