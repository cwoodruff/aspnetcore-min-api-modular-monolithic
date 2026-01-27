using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Modules.Endpoints;
using Orders.Modules.Services;
using SharedKernel;

namespace Orders.Modules;

public static class OrdersModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Orders";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Register module-specific services
            services.AddScoped<IInvoiceService, InvoiceService>();
            services.AddScoped<IInvoiceLineService, InvoiceLineService>();
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/orders");

            // Delegate to endpoint classes
            group.MapOrdersHealthEndpoints();
            group.MapOrdersDataHealthEndpoints();
            group.MapInvoiceEndpoints();
            group.MapInvoiceLineEndpoints();
        }
    }
}
