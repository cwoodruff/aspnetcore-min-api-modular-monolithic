using Admin.Modules.Data;
using Admin.Modules.Endpoints;
using Admin.Modules.Services;
using Admin.Modules.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using SharedKernel.Persistence;

namespace Admin.Modules;

public static class AdministrationModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Administration";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            services.AddModuleDbContext<AdministrationDbContext>(AdministrationDbContext.Schema);
            services.AddValidatorsFromAssemblyContaining<CustomerValidator>(includeInternalTypes: true);

            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IGenreService, GenreService>();
            services.AddScoped<IEmployeeService, EmployeeService>();
            services.AddScoped<IMediaTypeService, MediaTypeService>();
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/admin");

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
