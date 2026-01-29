# Health Checks & Readiness Probes Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

Health checks enable monitoring systems, load balancers, and container
orchestrators to determine if your application is healthy and ready to receive
traffic. ASP.NET Core provides built-in support for health check endpoints.

**Duration:** 30-45 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of deployment
concepts

---

## Learning Objectives

By the end of this guide, you will:

- Understand liveness vs. readiness probes
- Implement database health checks
- Create custom health checks
- Configure health check UI
- Integrate with Kubernetes and Docker

---

## 1. Understanding Health Check Types

### Liveness vs. Readiness

| Type          | Purpose                      | Example Failure                  |
|---------------|------------------------------|----------------------------------|
| **Liveness**  | Is the app running?          | Deadlock, infinite loop          |
| **Readiness** | Can the app handle requests? | Database down, cache unavailable |
| **Startup**   | Has the app started?         | Initial data loading             |

### Kubernetes Probe Mapping

```yaml
# Kubernetes deployment
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5

startupProbe:
  httpGet:
    path: /health/startup
    port: 8080
  failureThreshold: 30
  periodSeconds: 10
```

---

## 2. Basic Health Check Setup

### Minimal Configuration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Map health endpoint
app.MapHealthChecks("/health");

app.Run();
```

### Response

```http
GET /health
HTTP/1.1 200 OK
Content-Type: text/plain

Healthy
```

---

## 3. Database Health Checks

### Entity Framework Core DbContext Check

```csharp
// Add NuGet: Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "db" });
```

### Custom SQL Query Check

```csharp
builder.Services.AddHealthChecks()
    .AddSqlite(
        connectionString: builder.Configuration.GetConnectionString("AppDatabase")!,
        healthQuery: "SELECT 1",
        name: "sqlite",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "db" });
```

### SQL Server Check

```csharp
// Add NuGet: AspNetCore.HealthChecks.SqlServer

builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("SqlServer")!,
        healthQuery: "SELECT 1",
        name: "sqlserver",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "db" });
```

---

## 4. Multiple Health Checks

```csharp
builder.Services.AddHealthChecks()
    // Database
    .AddDbContextCheck<AppDbContext>(
        name: "database",
        tags: new[] { "ready", "db" })

    // Redis cache
    .AddRedis(
        redisConnectionString: builder.Configuration.GetConnectionString("Redis")!,
        name: "redis",
        tags: new[] { "ready", "cache" })

    // External API dependency
    .AddUrlGroup(
        uri: new Uri("https://api.external-service.com/health"),
        name: "external-api",
        tags: new[] { "ready", "external" })

    // Memory check
    .AddCheck<MemoryHealthCheck>(
        name: "memory",
        tags: new[] { "live" })

    // Disk space
    .AddDiskStorageHealthCheck(
        setup: opt => opt.AddDrive("C:\\", 1024), // 1GB minimum
        name: "disk",
        tags: new[] { "ready" });
```

---

## 5. Custom Health Checks

### Simple Health Check

```csharp
// HealthChecks/MemoryHealthCheck.cs
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _threshold;

    public MemoryHealthCheck(long thresholdMB = 1024)
    {
        _threshold = thresholdMB * 1024 * 1024;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var allocated = GC.GetTotalMemory(forceFullCollection: false);

        var data = new Dictionary<string, object>
        {
            ["AllocatedMB"] = allocated / 1024 / 1024,
            ["ThresholdMB"] = _threshold / 1024 / 1024,
            ["Gen0Collections"] = GC.CollectionCount(0),
            ["Gen1Collections"] = GC.CollectionCount(1),
            ["Gen2Collections"] = GC.CollectionCount(2)
        };

        if (allocated > _threshold)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                description: $"Memory usage ({allocated / 1024 / 1024}MB) exceeds threshold ({_threshold / 1024 / 1024}MB)",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            description: $"Memory usage: {allocated / 1024 / 1024}MB",
            data: data));
    }
}

// Registration
builder.Services.AddHealthChecks()
    .AddCheck<MemoryHealthCheck>("memory", tags: new[] { "live" });
```

### Cache Health Check

```csharp
// HealthChecks/CacheHealthCheck.cs
public class CacheHealthCheck : IHealthCheck
{
    private readonly ICacheFacade _cache;
    private readonly ILogger<CacheHealthCheck> _logger;

    public CacheHealthCheck(ICacheFacade cache, ILogger<CacheHealthCheck> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var testKey = $"health-check:{Guid.NewGuid()}";
            var testValue = DateTime.UtcNow.ToString("O");

            // Test write
            await _cache.SetAsync(
                testKey,
                testValue,
                new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5) },
                cancellationToken);

            // Test read
            var retrieved = await _cache.GetAsync<string>(testKey, cancellationToken);

            // Test delete
            await _cache.RemoveAsync(testKey, cancellationToken);

            if (retrieved == testValue)
            {
                return HealthCheckResult.Healthy("Cache read/write successful");
            }

            return HealthCheckResult.Degraded("Cache returned unexpected value");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache health check failed");
            return HealthCheckResult.Unhealthy(
                description: "Cache is unavailable",
                exception: ex);
        }
    }
}
```

### External Service Health Check

```csharp
// HealthChecks/ExternalApiHealthCheck.cs
public class ExternalApiHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiUrl;
    private readonly TimeSpan _timeout;

    public ExternalApiHealthCheck(
        IHttpClientFactory httpClientFactory,
        string apiUrl,
        TimeSpan? timeout = null)
    {
        _httpClientFactory = httpClientFactory;
        _apiUrl = apiUrl;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = _timeout;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await client.GetAsync(_apiUrl, cancellationToken);
            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["Url"] = _apiUrl,
                ["StatusCode"] = (int)response.StatusCode,
                ["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds
            };

            if (response.IsSuccessStatusCode)
            {
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    return HealthCheckResult.Degraded(
                        description: $"API responded slowly ({stopwatch.ElapsedMilliseconds}ms)",
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    description: $"API responded in {stopwatch.ElapsedMilliseconds}ms",
                    data: data);
            }

            return HealthCheckResult.Unhealthy(
                description: $"API returned {response.StatusCode}",
                data: data);
        }
        catch (TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                description: $"API request timed out after {_timeout.TotalSeconds}s");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                description: "API is unreachable",
                exception: ex);
        }
    }
}
```

---

## 6. Separate Liveness and Readiness Endpoints

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks()
    // Liveness checks - lightweight, always pass if app is running
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<MemoryHealthCheck>("memory", tags: new[] { "live" })

    // Readiness checks - verify dependencies
    .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" })
    .AddCheck<CacheHealthCheck>("cache", tags: new[] { "ready" })
    .AddCheck<ExternalApiHealthCheck>("external-api", tags: new[] { "ready" });

var app = builder.Build();

// Liveness - is the app running?
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteJsonResponse
});

// Readiness - can the app handle requests?
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteJsonResponse
});

// Full health check - all checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteJsonResponse
});

app.Run();
```

---

## 7. JSON Response Writer

```csharp
// HealthChecks/HealthCheckResponseWriter.cs
public static class HealthCheckResponseWriter
{
    public static Task WriteJsonResponse(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data,
                exception = e.Value.Exception?.Message,
                tags = e.Value.Tags
            })
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}

// Usage in Program.cs
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});
```

### Sample JSON Response

```json
{
  "status": "Healthy",
  "totalDuration": 125.4,
  "timestamp": "2024-01-15T10:30:00Z",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": null,
      "duration": 45.2,
      "data": {},
      "exception": null,
      "tags": ["ready", "db"]
    },
    {
      "name": "cache",
      "status": "Healthy",
      "description": "Cache read/write successful",
      "duration": 12.8,
      "data": {},
      "exception": null,
      "tags": ["ready"]
    },
    {
      "name": "memory",
      "status": "Healthy",
      "description": "Memory usage: 256MB",
      "duration": 0.5,
      "data": {
        "AllocatedMB": 256,
        "ThresholdMB": 1024,
        "Gen0Collections": 15,
        "Gen1Collections": 3,
        "Gen2Collections": 1
      },
      "exception": null,
      "tags": ["live"]
    }
  ]
}
```

---

## 8. Health Check UI

### Add Health Check UI

```csharp
// Add NuGet packages:
// AspNetCore.HealthChecks.UI
// AspNetCore.HealthChecks.UI.Client
// AspNetCore.HealthChecks.UI.InMemory.Storage

builder.Services.AddHealthChecksUI(options =>
{
    options.SetEvaluationTimeInSeconds(30);
    options.MaximumHistoryEntriesPerEndpoint(50);
    options.AddHealthCheckEndpoint("API", "/health");
})
.AddInMemoryStorage();

var app = builder.Build();

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Health check UI
app.MapHealthChecksUI(options =>
{
    options.UIPath = "/health-ui";
    options.ApiPath = "/health-api";
});
```

### appsettings.json Configuration

```json
{
  "HealthChecksUI": {
    "HealthChecks": [
      {
        "Name": "Modular Monolith API",
        "Uri": "/health"
      }
    ],
    "EvaluationTimeInSeconds": 30,
    "MinimumSecondsBetweenFailureNotifications": 60
  }
}
```

---

## 9. Webhook Notifications

```csharp
builder.Services.AddHealthChecksUI(options =>
{
    options.AddWebhookNotification("slack",
        uri: builder.Configuration["HealthChecks:SlackWebhook"]!,
        payload: """
        {
            "text": "Health Check Alert",
            "attachments": [
                {
                    "color": "[[FAILURE_COLOR]]",
                    "title": "[[LIVENESS]] is [[FAILURE_STATUS]]",
                    "text": "[[FAILURE_DESCRIPTION]]"
                }
            ]
        }
        """,
        restorePayload: """
        {
            "text": "Health Check Restored",
            "attachments": [
                {
                    "color": "#36a64f",
                    "title": "[[LIVENESS]] is healthy again"
                }
            ]
        }
        """);
})
.AddInMemoryStorage();
```

---

## 10. Startup Health Check

For applications with slow startup (loading caches, warming up):

```csharp
// HealthChecks/StartupHealthCheck.cs
public class StartupHealthCheck : IHealthCheck
{
    private volatile bool _isReady = false;

    public bool IsReady
    {
        get => _isReady;
        set => _isReady = value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_isReady)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Application started"));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("Application is starting"));
    }
}

// Registration
builder.Services.AddSingleton<StartupHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<StartupHealthCheck>("startup", tags: new[] { "startup" });

// Startup background service
public class StartupBackgroundService : BackgroundService
{
    private readonly StartupHealthCheck _healthCheck;
    private readonly IServiceProvider _services;
    private readonly ILogger<StartupBackgroundService> _logger;

    public StartupBackgroundService(
        StartupHealthCheck healthCheck,
        IServiceProvider services,
        ILogger<StartupBackgroundService> logger)
    {
        _healthCheck = healthCheck;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Application starting, warming up caches...");

        try
        {
            // Warm up caches
            using var scope = _services.CreateScope();
            var genreService = scope.ServiceProvider.GetRequiredService<IGenreService>();
            var artistService = scope.ServiceProvider.GetRequiredService<IArtistService>();

            await genreService.GetAllAsync(stoppingToken);
            await artistService.GetAllAsync(stoppingToken);

            _healthCheck.IsReady = true;
            _logger.LogInformation("Application startup complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application startup failed");
            throw;
        }
    }
}

builder.Services.AddHostedService<StartupBackgroundService>();

// Startup probe endpoint
app.MapHealthChecks("/health/startup", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("startup")
});
```

---

## 11. Docker Integration

### Dockerfile with Health Check

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Add curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ModularMonolith.Api.dll"]
```

### Docker Compose

```yaml
version: '3.8'

services:
  api:
    build: .
    ports:
      - "5043:8080"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/live"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s
    depends_on:
      redis:
        condition: service_healthy
      db:
        condition: service_healthy

  redis:
    image: redis:alpine
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 3

  db:
    image: postgres:15
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 3
```

---

## 12. Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: modular-monolith-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: modular-monolith-api
  template:
    metadata:
      labels:
        app: modular-monolith-api
    spec:
      containers:
      - name: api
        image: modular-monolith-api:latest
        ports:
        - containerPort: 8080

        # Startup probe - for slow-starting containers
        startupProbe:
          httpGet:
            path: /health/startup
            port: 8080
          failureThreshold: 30
          periodSeconds: 10

        # Liveness probe - restart if unhealthy
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3

        # Readiness probe - remove from load balancer if not ready
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 3

        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
```

---

## 13. Complete Implementation

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add health check services
builder.Services.AddSingleton<StartupHealthCheck>();

builder.Services.AddHealthChecks()
    // Liveness checks
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<MemoryHealthCheck>("memory", tags: new[] { "live" })

    // Readiness checks
    .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready", "db" })
    .AddCheck<CacheHealthCheck>("cache", tags: new[] { "ready", "cache" })

    // Startup check
    .AddCheck<StartupHealthCheck>("startup", tags: new[] { "startup" });

// Add health checks UI (optional)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHealthChecksUI(options =>
    {
        options.SetEvaluationTimeInSeconds(30);
        options.MaximumHistoryEntriesPerEndpoint(50);
    })
    .AddInMemoryStorage();
}

var app = builder.Build();

// Health endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});

app.MapHealthChecks("/health/startup", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("startup"),
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});

if (app.Environment.IsDevelopment())
{
    app.MapHealthChecksUI(options => options.UIPath = "/health-ui");
}

app.Run();
```

---

## Summary

### Health Check Endpoints

| Endpoint          | Purpose                        | Tags      |
|-------------------|--------------------------------|-----------|
| `/health/live`    | Is the app running?            | `live`    |
| `/health/ready`   | Can the app handle requests?   | `ready`   |
| `/health/startup` | Has the app finished starting? | `startup` |
| `/health`         | All checks combined            | all       |

### Best Practices

1. **Keep liveness checks lightweight** — Don't check dependencies
2. **Use readiness for dependencies** — Database, cache, external APIs
3. **Set appropriate timeouts** — Don't let health checks hang
4. **Log health check failures** — For debugging and alerting
5. **Return detailed data** — Help with troubleshooting
6. **Secure admin endpoints** — Don't expose sensitive health data publicly

### NuGet Packages

```xml
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI" Version="8.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI.Client" Version="8.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI.InMemory.Storage" Version="8.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Redis" Version="8.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="8.0.0" />
```
