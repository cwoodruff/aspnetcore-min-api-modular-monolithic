using Admin.Modules;
using Catalog.Modules;
using Identity.Modules;
using Identity.Modules.Extensions;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
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
    /// <summary>The demo environment: Development's conveniences, with its own appsettings.Demo.json.</summary>
    public const string DemoEnvironment = "Demo";

    /// <summary>
    /// Registers host services, then every module's services, and returns the modules in
    /// registration order so the caller can map their endpoints.
    /// </summary>
    public static IReadOnlyList<IModule> ConfigureServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        // Demo behaves like Development (in-memory logins, Swagger), but CreateBuilder loads user secrets in
        // Development only. Load them in Demo too, where Development has them: after appsettings.{env}.json,
        // before environment variables, so an environment variable still wins.
        if (builder.Environment.IsEnvironment(DemoEnvironment))
        {
            AddUserSecretsBeforeEnvironmentVariables(configuration);
        }

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

            // One document per module, holding the endpoints tagged with that module's name.
            foreach (var (document, tag) in ModuleOpenApiDocuments)
            {
                c.SwaggerDoc(document, new OpenApiInfo
                {
                    Title = $"Modular Monolith API: {tag}",
                    Version = "v1",
                    Description = $"The {tag} module's endpoints only."
                });
            }

            c.DocInclusionPredicate((document, api) =>
                document == "v1"
                || (ModuleOpenApiDocuments.TryGetValue(document, out var tag)
                    && api.ActionDescriptor.EndpointMetadata.OfType<ITagsMetadata>().Any(tags => tags.Tags.Contains(tag))));

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
        services.AddModuleRateLimitPolicy(RateLimitPolicyRegistry.GlobalPublicAnon);

        // JSON for the outbox and the shared L2 cache. The reflection resolver is fine for this host, which
        // is not trimmed; a Native AOT host would register source-generated type information here instead.
        services.AddReflectionJsonSerialization();
        services.AddHealthChecks();

        var modules = GetModules();
        foreach (var module in modules)
        {
            module.RegisterServices(services, configuration);
        }

        return modules;
    }

    /// <summary>OpenAPI document name per module, mapped to the tag its endpoints carry.</summary>
    public static readonly IReadOnlyDictionary<string, string> ModuleOpenApiDocuments = new Dictionary<string, string>
    {
        ["catalog"] = "Catalog",
        ["orders"] = "Orders",
        ["admin"] = "Administration",
        ["identity"] = "Identity",
        ["reporting"] = "Reporting"
    };

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

    private static void AddUserSecretsBeforeEnvironmentVariables(ConfigurationManager configuration)
    {
        configuration.AddUserSecrets(typeof(HostComposition).Assembly, optional: true);
        var secrets = configuration.Sources[^1];
        // The unprefixed environment variables CreateBuilder adds after the appsettings files, not the
        // ASPNETCORE_/DOTNET_ ones it adds first for the host.
        var environmentVariables = configuration.Sources.ToList().FindLastIndex(source =>
            source is EnvironmentVariablesConfigurationSource { Prefix: null or "" });
        if (environmentVariables >= 0)
        {
            configuration.Sources.RemoveAt(configuration.Sources.Count - 1);
            configuration.Sources.Insert(environmentVariables, secrets);
        }
    }
}
