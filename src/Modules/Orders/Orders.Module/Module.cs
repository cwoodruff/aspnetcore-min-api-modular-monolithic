using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Modules.Data;
using Orders.Modules.Endpoints;
using Orders.Modules.Events;
using Orders.Modules.Services;
using Orders.Modules.Validation;
using SharedKernel;
using SharedKernel.Caching;
using SharedKernel.Diagnostics;
using SharedKernel.Events;
using SharedKernel.Persistence;
using SharedKernel.TrafficControl;

namespace Orders.Modules;

public static class OrdersModule
{
    /// <summary>The module's name and its key for keyed services, caches, metrics and rate limits.</summary>
    internal const string ModuleName = "Orders";

    public sealed class Modules : IModule
    {
        public string Name => ModuleName;

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            services.AddModuleDbContext<OrdersDbContext>(ModuleName, OrdersDbContext.Schema);
            services.AddModuleCache(ModuleName, config.GetValue($"Caching:Modules:{ModuleName}:SizeLimit", 1000));
            services.AddModuleRateLimitPolicy(RateLimitPolicyRegistry.Orders);
            services.AddValidatorsFromAssemblyContaining<InvoiceValidator>(includeInternalTypes: true);

            services.AddScoped<IInvoiceService, InvoiceService>();
            services.AddScoped<IInvoiceLineService, InvoiceLineService>();
            services.AddScoped<DeadLetterService>();

            services.AddEventPublisher(ModuleName);
            services.AddOutboxDispatcher<OrdersOutboxDispatcher>(ModuleName);
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/orders")
                .RequireRateLimiting(RateLimitPolicyRegistry.Orders)
                .AddModuleMetrics(ModuleName);

            // Delegate to endpoint classes
            group.MapOrdersHealthEndpoints();
            group.MapOrdersDataHealthEndpoints();
            group.MapInvoiceEndpoints();
            group.MapInvoiceLineEndpoints();
            group.MapOutboxEndpoints();
        }
    }
}
