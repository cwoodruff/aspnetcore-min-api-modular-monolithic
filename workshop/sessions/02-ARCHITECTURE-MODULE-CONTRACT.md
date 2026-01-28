# Session 2: Architecture Overview & Module Contract

**Duration:** 60 minutes
**Session Time:** 8:30 AM - 9:30 AM

---

## Overview

This session dives deep into the Modular Monolithic architecture pattern. You'll understand why this pattern is valuable, how the IModule contract enables clean composition, and how the host application orchestrates all modules.

---

## Learning Objectives

By the end of this session, you will:
- Understand Modular Monolithic architecture benefits and trade-offs
- Know when to choose this pattern over alternatives
- Implement the IModule contract pattern
- Configure the host for module composition
- Understand module boundaries and isolation

---

## Part 1: Architecture Concepts (20 minutes)

### 1.1 Architecture Comparison

| Aspect | Traditional Monolith | Modular Monolith | Microservices |
|--------|---------------------|------------------|---------------|
| **Deployment** | Single unit | Single unit | Independent services |
| **Boundaries** | None/weak | Strong (compile-time) | Strong (runtime) |
| **Communication** | Direct method calls | Direct method calls | Network (HTTP/messaging) |
| **Data** | Shared database | Shared DB (logical separation) | Per-service database |
| **Complexity** | Low | Medium | High |
| **Team Scaling** | Limited | Good | Excellent |
| **Latency** | Lowest | Low | Higher (network) |
| **Operational Cost** | Low | Low | High |

### 1.2 When to Choose Modular Monolith

**Ideal Scenarios:**
- Starting a new project with uncertain domain boundaries
- Team size: 3-15 developers
- Need rapid development with clean architecture
- Plan to potentially extract microservices later
- Performance-critical applications (no network overhead)

**Not Ideal When:**
- Different modules need different scaling profiles
- Independent deployment of modules is required
- Teams need complete autonomy with technology choices
- Module failure isolation is critical

### 1.3 Key Benefits of Our Approach

1. **Compile-Time Boundaries** - Module violations caught during build
2. **Shared Infrastructure** - Reuse authentication, caching, logging
3. **Simple Deployment** - Single artifact to deploy
4. **Easy Refactoring** - IDE support across module boundaries
5. **Future Extraction** - Can evolve to microservices when needed

---

## Part 2: The IModule Contract (15 minutes)

### 2.1 Contract Definition

The IModule interface is the cornerstone of our architecture:

**File: `src/Shared/SharedKernel/IModule.cs`**

```csharp
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel;

public interface IModule
{
    string Name { get; }
    void RegisterServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

### 2.2 Contract Responsibilities

| Method | Purpose | When Called |
|--------|---------|-------------|
| `Name` | Module identifier for logging/monitoring | Build/Runtime |
| `RegisterServices` | Register DI services | App startup (before Build) |
| `MapEndpoints` | Define API routes | App startup (after Build) |

### 2.3 Design Principles

1. **Self-Contained** - Modules register their own services
2. **No Cross-References** - Modules don't reference each other
3. **Host Orchestration** - Only the host knows about all modules
4. **Internal by Default** - Module internals stay internal

### 2.4 Module Project Structure

Each module follows this structure:

```
/Modules/Music/Music.Module/
├── Module.cs                 # IModule implementation
├── Endpoints/                # API endpoint definitions
│   ├── AlbumEndpoints.cs
│   ├── ArtistEndpoints.cs
│   ├── HealthEndpoints.cs
│   └── DataHealthEndpoints.cs
└── Services/                 # Business logic services
    ├── IAlbumService.cs
    ├── AlbumService.cs
    ├── IArtistService.cs
    └── ArtistService.cs
```

---

## Part 3: Host Composition (25 minutes)

### 3.1 Complete Program.cs

This is the complete host composition from the solution:

**File: `src/ModularMonolith.Api/Program.cs`**

```csharp
using System.Reflection;
using Admin.Modules;
using Identity.Modules;
using Identity.Modules.Extensions;
using Microsoft.AspNetCore.Http.Json;
using Music.Modules;
using Orders.Modules;
using Reporting.Modules;
using SharedKernel;
using SharedKernel.Caching;
using SharedKernel.Persistence;
using SharedKernel.TrafficControl;
using System.Threading.RateLimiting;
using Microsoft.OpenApi;
using SharedKernel.DataSQLite.Repositories;
using SharedKernel.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null; // keep exact casing
});

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Modular Monolith API",
        Version = "v1",
        Description = "ASP.NET Core Minimal API Modular Monolith with modules: Music, Orders, Administration, Reporting, Identity.",
        Contact = new OpenApiContact { Name = "API Team" }
    });

    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste your JWT access token only (no 'Bearer ' prefix). Swagger will add the prefix automatically.",
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
builder.Services.AddProblemDetails();

// EF Core persistence registration
// Resolve SQLite path for AppDbContext if not provided
var existing = builder.Configuration.GetConnectionString("AppDatabase")
              ?? builder.Configuration["ConnectionStrings:AppDatabase"]
              ?? Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase");
if (string.IsNullOrWhiteSpace(existing))
{
    static string? TryFindDb(string contentRoot)
    {
        var contentDb = Path.Combine(contentRoot, "data", "chinook.db");
        if (File.Exists(contentDb))
        {
            return contentDb;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "data")))
        {
            current = current.Parent;
        }
        var root = current?.FullName;
        var rootDb = root is not null ? Path.Combine(root, "data", "chinook.db") : null;
        return rootDb is not null && File.Exists(rootDb) ? rootDb : null;
    }
    var dbPath = TryFindDb(builder.Environment.ContentRootPath);
    if (!string.IsNullOrWhiteSpace(dbPath))
    {
        builder.Configuration["ConnectionStrings:AppDatabase"] = $"Data Source={dbPath}";
    }
}

// Data Repositories
builder.Services.AddScoped<IAlbumRepository, AlbumRepository>()
    .AddScoped<IArtistRepository, ArtistRepository>()
    .AddScoped<ICustomerRepository, CustomerRepository>()
    .AddScoped<IEmployeeRepository, EmployeeRepository>()
    .AddScoped<IGenreRepository, GenreRepository>()
    .AddScoped<IInvoiceRepository, InvoiceRepository>()
    .AddScoped<IInvoiceLineRepository, InvoiceLineRepository>()
    .AddScoped<IMediaTypeRepository, MediaTypeRepository>()
    .AddScoped<IPlaylistRepository, PlaylistRepository>()
    .AddScoped<ITrackRepository, TrackRepository>();

builder.Services.AddKernelPersistence(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy
            .WithOrigins(GetAllowedOrigins())
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Identity Auth registration (lives in Identity module)
builder.Services.AddIdentityAuth(builder.Configuration);

// Central caching registration (L1 IMemoryCache by default; L2 if configured)
builder.Services.AddCentralCaching(builder.Configuration);

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicyRegistry.Names.GlobalPublicAnon, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: PartitionKeys.FromRequest(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Register module services BEFORE building the app
var modules = GetModules();
foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Default");

// Rate limiter should run early in the pipeline
app.UseRateLimiter();

// AuthN/AuthZ middleware from Identity module
app.UseIdentityAuth();

app.UseSwagger();
app.UseSwaggerUI();

// Root endpoint with service metadata
app.MapGet("/", (IConfiguration cfg, IWebHostEnvironment env) =>
{
    var response = new
    {
        module = "root",
        status = "Healthy",
        timestampUtc = DateTime.UtcNow.ToString("O"),
        environment = env.EnvironmentName,
        version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                  ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                  ?? "1.0.0",
        service = cfg["ServiceName"] ?? "ModularMonolith.Api",
    };
    return Results.Json(response);
})
.WithName("Root")
.Produces(200)
.WithTags("Root")
.Produces(429)
.RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

// Map module endpoints
app.MapGroup("");
foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

static IReadOnlyList<IModule> GetModules()
{
    return
    [
        new AdministrationModule.Modules(),
        new IdentityModule.Modules(),
        new MusicModule.Modules(),
        new OrdersModule.Modules(),
        new ReportingModule.Modules()
    ];
}

static string[] GetAllowedOrigins() =>
[
    "http://localhost:3000", "http://localhost:4200", "http://localhost:5173",
    "https://localhost:3000", "https://localhost:4200", "https://localhost:5173"
];

// For WebApplicationFactory
#pragma warning disable ASP0027
namespace ModularMonolith.Api
{
    public partial class Program { }
}
#pragma warning restore ASP0027
```

### 3.2 Key Composition Patterns

#### Pattern 1: Service Registration Before Build

```csharp
// BEFORE app.Build()
foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();  // Services are locked after this
```

#### Pattern 2: Endpoint Mapping After Build

```csharp
// AFTER app.Build()
foreach (var module in modules)
{
    module.MapEndpoints(app);
}
```

#### Pattern 3: Module Discovery

```csharp
static IReadOnlyList<IModule> GetModules()
{
    return
    [
        new AdministrationModule.Modules(),
        new IdentityModule.Modules(),
        new MusicModule.Modules(),
        new OrdersModule.Modules(),
        new ReportingModule.Modules()
    ];
}
```

### 3.3 Module Implementation Examples

#### Music Module

**File: `src/Modules/Music/Music.Module/Module.cs`**

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Music.Modules.Endpoints;
using Music.Modules.Services;
using SharedKernel;

namespace Music.Modules;

public static class MusicModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Music";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Register module-specific services
            services.AddScoped<IAlbumService, AlbumService>();
            services.AddScoped<IArtistService, ArtistService>();
            services.AddScoped<IPlaylistService, PlaylistService>();
            services.AddScoped<ITrackService, TrackService>();
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/music");

            // Delegate to endpoint classes
            group.MapMusicHealthEndpoints();
            group.MapMusicDataHealthEndpoints();
            group.MapAlbumEndpoints();
            group.MapArtistEndpoints();
            group.MapTrackEndpoints();
            group.MapPlaylistEndpoints();
        }
    }
}
```

#### Administration Module

**File: `src/Modules/Administration/Admin.Module/Module.cs`**

```csharp
using Admin.Modules.Endpoints;
using Admin.Modules.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Admin.Modules;

public static class AdministrationModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Administration";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Register module-specific services
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
```

#### Orders Module

**File: `src/Modules/Orders/Orders.Module/Module.cs`**

```csharp
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
```

#### Reporting Module

**File: `src/Modules/Reporting/Reporting.Module/Module.cs`**

```csharp
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
```

#### Identity Module

**File: `src/Modules/Identity/Identity.Module/Module.cs`**

```csharp
using Identity.Modules.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Identity.Modules;

public static class IdentityModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Identity";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Register module-specific services here in the future
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/identity");

            // Delegate to endpoint classes
            group.MapIdentityHealthEndpoints();
            group.MapIdentityDataHealthEndpoints();
            AuthEndpoints.MapIdentityAuthEndpoints(group);
        }
    }
}
```

---

## Part 4: Shared Infrastructure

### 4.1 BuildInfoProvider

Utility for consistent health endpoint responses:

**File: `src/Shared/SharedKernel/BuildInfoProvider.cs`**

```csharp
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SharedKernel;

public static class BuildInfoProvider
{
    public static string GetInformationalVersion(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return infoVersion ?? assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    public static string GetEnvironment(IHostEnvironment env)
        => env.EnvironmentName;

    public static string GetServiceName(IConfiguration config)
        => config["ServiceName"] ?? "ModularMonolith.Api";
}
```

---

## Checkpoint

Before moving to Session 3, verify:

- [ ] Understand Modular Monolith vs Microservices trade-offs
- [ ] Know when to choose Modular Monolith
- [ ] Understand the IModule contract methods
- [ ] Know the composition order (services before build, endpoints after)
- [ ] Can identify the structure of a module implementation
- [ ] Solution runs with all 5 modules loaded

---

## Quick Reference

### Module Implementation Template

```csharp
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace YourNamespace.Modules;

public static class YourModule
{
    public sealed class Modules : IModule
    {
        public string Name => "YourModule";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // Register your services here
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/your-route");
            // Map your endpoints here
        }
    }
}
```

### URL Patterns by Module

| Module | Base URL | Example Endpoints |
|--------|----------|-------------------|
| Music | `/api/music` | `/albums`, `/artists`, `/tracks` |
| Orders | `/api/orders` | `/invoices`, `/invoice-lines` |
| Administration | `/api/admin` | `/customers`, `/employees`, `/genres` |
| Reporting | `/api/reporting` | `/health`, `/data-health` |
| Identity | `/api/identity` | `/login`, `/refresh`, `/userinfo` |

---

## Next Session

In **Session 3: Building Your First Module**, you will:
- Create health and data-health endpoints
- Understand endpoint metadata configuration
- Implement the complete Reporting module
- Apply rate limiting to endpoints
