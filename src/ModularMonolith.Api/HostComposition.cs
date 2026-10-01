using System.Threading.RateLimiting;
using Admin.Modules;
using Catalog.Modules;
using Identity.Modules;
using Identity.Modules.Extensions;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi;
using Orders.Modules;
using Reporting.Modules;
using SharedKernel;
using SharedKernel.Caching;
using SharedKernel.DataSQLite.Repositories;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Repositories;
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

        // EF Core persistence registration
        // Resolve SQLite path for AppDbContext if not provided via configuration/environment.
        var existing = configuration.GetConnectionString("AppDatabase")
                       ?? configuration["ConnectionStrings:AppDatabase"]
                       ?? Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase");
        if (string.IsNullOrWhiteSpace(existing))
        {
            static bool HasUsableDb(string path)
            {
                return File.Exists(path) && new FileInfo(path).Length > 0;
            }

            static string? TryFindDb(string contentRoot)
            {
                var contentDb = Path.Combine(contentRoot, "data", "chinook.db");
                if (HasUsableDb(contentDb))
                {
                    return contentDb;
                }

                var current = new DirectoryInfo(AppContext.BaseDirectory);
                while (current is not null && !HasUsableDb(Path.Combine(current.FullName, "data", "chinook.db")))
                {
                    current = current.Parent;
                }

                var root = current?.FullName;
                var rootDb = root is not null ? Path.Combine(root, "data", "chinook.db") : null;
                return rootDb is not null && HasUsableDb(rootDb) ? rootDb : null;
            }

            var dbPath = TryFindDb(builder.Environment.ContentRootPath);
            if (!string.IsNullOrWhiteSpace(dbPath))
            {
                configuration["ConnectionStrings:AppDatabase"] = $"Data Source={dbPath}";
            }
        }

        // Data Repositories
        services.AddScoped<IAlbumRepository, AlbumRepository>()
            .AddScoped<IArtistRepository, ArtistRepository>()
            .AddScoped<ICustomerRepository, CustomerRepository>()
            .AddScoped<IEmployeeRepository, EmployeeRepository>()
            .AddScoped<IGenreRepository, GenreRepository>()
            .AddScoped<IInvoiceRepository, InvoiceRepository>()
            .AddScoped<IInvoiceLineRepository, InvoiceLineRepository>()
            .AddScoped<IMediaTypeRepository, MediaTypeRepository>()
            .AddScoped<IPlaylistRepository, PlaylistRepository>()
            .AddScoped<ITrackRepository, TrackRepository>();

        services.AddKernelPersistence(configuration);

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

        // Central caching registration (L1 IMemoryCache by default; L2 if configured)
        services.AddCentralCaching(configuration);

        // Option A: Minimal in-app rate limiting wiring
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RateLimitPolicyRegistry.Names.GlobalPublicAnon, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKeys.FromRequest(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60, // 60 requests per 60 seconds
                        Window = TimeSpan.FromSeconds(60),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

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
