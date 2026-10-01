using Admin.Modules;
using Catalog.Modules;
using Identity.Modules;
using Identity.Modules.Extensions;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi;
using Orders.Modules;
using Reporting.Modules;
using SharedKernel;
using SharedKernel.TrafficControl;

namespace ModularMonolith.Api;

/// <summary>
/// Service registration for the host, extracted from Program.cs so architecture tests can build
/// the same container the application runs with.
/// </summary>
public static class HostComposition
{
    /// <summary>
    /// Registers host services, then every module's services, and returns the modules in
    /// registration order so the caller can map their endpoints.
    /// </summary>
    public static IReadOnlyList<IModule> ConfigureServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        // Configuration
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = null; // keep exact casing provided in anonymous objects
        });

        // Services
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Modular Monolith API",
                Version = "v1",
                Description =
                    "ASP.NET Core Minimal API Modular Monolith with modules: Catalog, Orders, Administration, Reporting, Identity.",
                Contact = new OpenApiContact { Name = "API Team" }
            });

            var jwtSecurityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description =
                    "Paste your JWT access token only (no 'Bearer ' prefix). Swagger will add the prefix automatically.",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            };

            c.AddSecurityDefinition("Bearer", jwtSecurityScheme);
            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
                [new OpenApiSecuritySchemeReference("X-API-Key", document)] = []
            });
        });
        services.AddProblemDetails();

        // Each module registers its own DbContext in RegisterServices (ADR-0003); the host registers none.

        services.AddCors(options =>
        {
            options.AddPolicy("Default", policy =>
                policy
                    .WithOrigins(GetAllowedOrigins())
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        // Identity Auth registration (lives in Identity module)
        services.AddIdentityAuth(configuration);

        // Rate limiting: the root endpoint's policy here; each module adds and applies its own (ADR-0013).
        // Each module also registers its own cache, meter and health check in RegisterServices.
        services.AddModuleRateLimitPolicy(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
        services.AddHealthChecks();

        var modules = GetModules();
        foreach (var module in modules)
        {
            module.RegisterServices(services, configuration);
        }

        return modules;
    }

    public static IReadOnlyList<IModule> GetModules()
    {
        return
        [
            new AdministrationModule.Modules(),
            new IdentityModule.Modules(),
            new CatalogModule.Modules(),
            new OrdersModule.Modules(),
            new ReportingModule.Modules()
        ];
    }

    private static string[] GetAllowedOrigins()
    {
        return
        [
            "http://localhost:3000", "http://localhost:4200", "http://localhost:5173",
            "https://localhost:3000", "https://localhost:4200", "https://localhost:5173"
        ];
    }
}
