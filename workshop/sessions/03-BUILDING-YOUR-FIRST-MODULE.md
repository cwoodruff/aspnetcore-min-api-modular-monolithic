# Session 3: Building Your First Module

**Duration:** 60 minutes
**Session Time:** 9:30 AM - 10:30 AM

---

## Overview

In this session, you'll build complete module implementations with health endpoints, data-health endpoints, and proper endpoint metadata. You'll learn the patterns used throughout the solution for endpoint configuration.

---

## Learning Objectives

By the end of this session, you will:
- Create health endpoints that report module status
- Create data-health endpoints that verify database connectivity
- Configure endpoint metadata (names, tags, produces, rate limiting)
- Understand MapGroup for route prefixing
- Apply consistent patterns across modules

---

## Part 1: Health Endpoints Pattern (20 minutes)

### 1.1 Health Endpoint Purpose

Health endpoints provide:
- **Liveness checks** - Is the module running?
- **Readiness checks** - Can the module handle requests?
- **Monitoring data** - Version, environment, timestamps

### 1.2 Music Health Endpoint Implementation

**File: `src/Modules/Music/Music.Module/Endpoints/HealthEndpoints.cs`**

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace Music.Modules.Endpoints;

public static class MusicHealthEndpoints
{
    public static void MapMusicHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/health", (IHostEnvironment env, IConfiguration cfg) =>
            {
                var response = new
                {
                    module = "Music",
                    status = "Healthy",
                    timestampUtc = DateTime.UtcNow.ToString("O"),
                    environment = BuildInfoProvider.GetEnvironment(env),
                    version = BuildInfoProvider.GetInformationalVersion(typeof(MusicModule).Assembly),
                    service = BuildInfoProvider.GetServiceName(cfg),
                };
                return Results.Json(response);
            })
            .WithName("MusicHealth")
            .Produces(200)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
```

### 1.3 Endpoint Metadata Explained

| Method | Purpose |
|--------|---------|
| `.WithName("MusicHealth")` | Unique identifier for OpenAPI/link generation |
| `.Produces(200)` | Documents 200 OK response for Swagger |
| `.WithTags("Music")` | Groups endpoint in Swagger UI |
| `.Produces(429)` | Documents rate limit response |
| `.RequireRateLimiting(...)` | Applies rate limiting policy |

### 1.4 Extension Method Pattern

The `MapMusicHealthEndpoints` is an extension method on `IEndpointRouteBuilder`, allowing clean composition:

```csharp
// In Module.cs MapEndpoints:
var group = endpoints.MapGroup("/api/music");
group.MapMusicHealthEndpoints();  // Extension method called on group
```

---

## Part 2: Data-Health Endpoints (20 minutes)

### 2.1 Data-Health Purpose

Data-health endpoints verify:
- Database connectivity
- External service availability
- Configuration correctness

### 2.2 Music Data-Health Implementation

**File: `src/Modules/Music/Music.Module/Endpoints/DataHealthEndpoints.cs`**

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;
using SharedKernel.Persistence;

namespace Music.Modules.Endpoints;

public static class MusicDataHealthEndpoints
{
    public static void MapMusicDataHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/data-health", async (AppDbContext db, IHostEnvironment env, IConfiguration cfg, CancellationToken ct) =>
            {
                bool canConnect;
                try
                {
                    canConnect = await db.Database.CanConnectAsync(ct);
                }
                catch
                {
                    canConnect = false;
                }

                var response = new
                {
                    module = "Music",
                    status = canConnect ? "Data-Healthy" : "Degraded",
                    timestampUtc = DateTime.UtcNow.ToString("O"),
                    environment = BuildInfoProvider.GetEnvironment(env),
                    version = BuildInfoProvider.GetInformationalVersion(typeof(MusicModule).Assembly),
                    service = BuildInfoProvider.GetServiceName(cfg),
                    database = new { connected = canConnect }
                };
                return Results.Json(response);
            })
            .WithName("MusicDataHealth")
            .Produces(200)
            .WithTags("Music")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
```

### 2.3 Key Implementation Details

#### Async Handler with CancellationToken

```csharp
async (AppDbContext db, ..., CancellationToken ct) =>
{
    canConnect = await db.Database.CanConnectAsync(ct);
}
```

- `CancellationToken ct` - Automatically populated by ASP.NET Core
- Enables cancellation when client disconnects

#### Error Handling Pattern

```csharp
bool canConnect;
try
{
    canConnect = await db.Database.CanConnectAsync(ct);
}
catch
{
    canConnect = false;
}
```

- Silent failure for health checks
- Returns degraded status instead of exception

---

## Part 3: Complete Module Implementations (20 minutes)

### 3.1 Reporting Module (Simplest Example)

The Reporting module is the simplest - it only has health endpoints:

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

### 3.2 Rate Limiting Policy Registry

The solution uses a central registry for rate limit policy names:

**File: `src/Shared/SharedKernel/TrafficControl/RateLimitPolicyRegistry.cs`**

```csharp
using Microsoft.Extensions.Configuration;

namespace SharedKernel.TrafficControl;

/// <summary>
/// Central registry for rate limiting policy names and configuration binding.
/// </summary>
public sealed class RateLimitPolicyRegistry(IConfiguration configuration)
{
    // Canonical policy names (modules should reference these names only)
    public static class Names
    {
        public const string GlobalPublicAnon = "global:public-anon";
        public const string GlobalUserStandard = "global:user-standard";
        public const string GlobalTenantStandard = "global:tenant-standard";
        public const string GlobalAdminElevated = "global:admin-elevated";
        public const string ReportingHeavy = "reporting:heavy";
    }

    /// <summary>
    /// Placeholder for future binding of policies from configuration.
    /// </summary>
    public IConfiguration Section => configuration.GetSection("RateLimiting");
}
```

### 3.3 Partition Keys for Rate Limiting

**File: `src/Shared/SharedKernel/TrafficControl/PartitionKeys.cs`**

```csharp
using Microsoft.AspNetCore.Http;

namespace SharedKernel.TrafficControl;

/// <summary>
/// Helpers to derive a stable partition key for rate limiting from the current request.
/// </summary>
public static class PartitionKeys
{
    public static string FromRequest(HttpContext httpContext)
    {
        // Priority: API key/client_id -> tenant -> sub -> IP
        var clientId = httpContext.User.FindFirst("client_id")?.Value;
        if (!string.IsNullOrWhiteSpace(clientId)) return $"client:{Normalize(clientId)}";

        var tenant = httpContext.User.FindFirst("tenant")?.Value;
        if (!string.IsNullOrWhiteSpace(tenant)) return $"tenant:{Normalize(tenant)}";

        var sub = httpContext.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(sub)) return $"sub:{Normalize(sub)}";

        var ip = GetClientIp(httpContext);
        return $"ip:{ip}";
    }

    private static string GetClientIp(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        return ip is null ? "unknown" : ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? ip.MapToIPv4().ToString()
            : ip.ToString();
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
```

### 3.4 Module Registration in Program.cs

All modules are registered in the host:

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

---

## Part 4: Hands-On Exercise

### Exercise: Create Additional Health Endpoints

Create health endpoints for the Administration module following the exact patterns shown.

**Expected Files:**
- `src/Modules/Administration/Admin.Module/Endpoints/HealthEndpoints.cs`
- `src/Modules/Administration/Admin.Module/Endpoints/DataHealthEndpoints.cs`

**Requirements:**
1. Module name should be "Administration"
2. Route group should be `/api/admin`
3. Apply rate limiting policy
4. Include proper endpoint metadata

### Solution Reference

The Administration module already has these implementations in the solution.

---

## Testing Your Module

### 1. Run the Application

```bash
dotnet run --project src/ModularMonolith.Api
```

### 2. Test Health Endpoints

```bash
# Music health
curl http://localhost:5043/api/music/health

# Music data-health
curl http://localhost:5043/api/music/data-health

# Reporting health
curl http://localhost:5043/api/reporting/health

# Administration health
curl http://localhost:5043/api/admin/health
```

### 3. Expected Response Format

```json
{
  "module": "Music",
  "status": "Healthy",
  "timestampUtc": "2024-01-15T10:30:00.0000000Z",
  "environment": "Development",
  "version": "1.0.0",
  "service": "ModularMonolith.Api"
}
```

---

## Checkpoint

Before moving to Session 4, verify:

- [ ] Understand the health endpoint pattern
- [ ] Understand the data-health endpoint pattern
- [ ] Know how to configure endpoint metadata
- [ ] Understand MapGroup for route prefixes
- [ ] Know how to apply rate limiting
- [ ] Can create endpoints following the solution patterns
- [ ] All health endpoints return expected responses

---

## Quick Reference

### Health Endpoint Template

```csharp
public static void MapYourModuleHealthEndpoints(this IEndpointRouteBuilder group)
{
    group.MapGet("/health", (IHostEnvironment env, IConfiguration cfg) =>
        {
            var response = new
            {
                module = "YourModule",
                status = "Healthy",
                timestampUtc = DateTime.UtcNow.ToString("O"),
                environment = BuildInfoProvider.GetEnvironment(env),
                version = BuildInfoProvider.GetInformationalVersion(typeof(YourModule).Assembly),
                service = BuildInfoProvider.GetServiceName(cfg),
            };
            return Results.Json(response);
        })
        .WithName("YourModuleHealth")
        .Produces(200)
        .WithTags("YourModule")
        .Produces(429)
        .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
}
```

### Data-Health Endpoint Template

```csharp
public static void MapYourModuleDataHealthEndpoints(this IEndpointRouteBuilder group)
{
    group.MapGet("/data-health", async (AppDbContext db, IHostEnvironment env, IConfiguration cfg, CancellationToken ct) =>
        {
            bool canConnect;
            try { canConnect = await db.Database.CanConnectAsync(ct); }
            catch { canConnect = false; }

            var response = new
            {
                module = "YourModule",
                status = canConnect ? "Data-Healthy" : "Degraded",
                timestampUtc = DateTime.UtcNow.ToString("O"),
                environment = BuildInfoProvider.GetEnvironment(env),
                version = BuildInfoProvider.GetInformationalVersion(typeof(YourModule).Assembly),
                service = BuildInfoProvider.GetServiceName(cfg),
                database = new { connected = canConnect }
            };
            return Results.Json(response);
        })
        .WithName("YourModuleDataHealth")
        .Produces(200)
        .WithTags("YourModule")
        .Produces(429)
        .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
}
```

### Common Endpoint Metadata

| Method | Example |
|--------|---------|
| `.WithName(string)` | `.WithName("GetAlbumById")` |
| `.WithTags(params string[])` | `.WithTags("Music", "Albums")` |
| `.Produces(int)` | `.Produces(200)`, `.Produces(404)` |
| `.Produces<T>(int)` | `.Produces<AlbumApiModel>(200)` |
| `.ProducesValidationProblem()` | Adds 400 validation response |
| `.RequireAuthorization(string)` | `.RequireAuthorization("music.read")` |
| `.RequireRateLimiting(string)` | `.RequireRateLimiting("global:public-anon")` |
| `.AllowAnonymous()` | Bypasses authentication |

---

## Next Session

In **Session 4: Authentication & Authorization**, you will:
- Implement JWT Bearer authentication
- Create login and token endpoints
- Define authorization policies
- Protect endpoints with RequireAuthorization
- Understand multi-tenant authorization
