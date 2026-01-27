# OpenTelemetry Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

OpenTelemetry provides a vendor-neutral standard for collecting telemetry data (traces, metrics, logs) from your applications. It enables distributed tracing across microservices and provides insights into application performance.

**Duration:** 45-60 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of observability concepts

---

## Learning Objectives

By the end of this guide, you will:
- Understand traces, metrics, and logs in OpenTelemetry
- Configure automatic instrumentation for ASP.NET Core
- Add custom spans and attributes
- Export telemetry to various backends
- Set up Jaeger for distributed tracing visualization

---

## 1. Understanding OpenTelemetry

### Three Pillars of Observability

| Pillar | Purpose | Example |
|--------|---------|---------|
| **Traces** | Follow request flow | Request → Service A → Database → Service B |
| **Metrics** | Aggregate measurements | Request count, response time, CPU usage |
| **Logs** | Event records | Error messages, audit events |

### Key Concepts

```
Trace
  └── Span (HTTP Request)
        ├── Span (Database Query)
        └── Span (External API Call)
              └── Span (Serialization)
```

- **Trace**: End-to-end journey of a request
- **Span**: Single operation within a trace
- **Context**: Propagated data (trace ID, span ID)
- **Attributes**: Key-value metadata on spans

---

## 2. Setting Up OpenTelemetry

### Install Packages

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.10" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.7.0" />
```

### Basic Configuration

```csharp
// Program.cs
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "ModularMonolith.Api",
            serviceVersion: "1.0.0",
            serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();
```

---

## 3. Distributed Tracing with Jaeger

### Docker Compose

```yaml
version: '3.8'

services:
  api:
    build: .
    ports:
      - "5043:8080"
    environment:
      - OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317
    depends_on:
      - jaeger

  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"  # UI
      - "4317:4317"    # OTLP gRPC
      - "4318:4318"    # OTLP HTTP
    environment:
      - COLLECTOR_OTLP_ENABLED=true
```

### Export to Jaeger

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("ModularMonolith.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
        })
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(
                builder.Configuration["Otel:Endpoint"]
                ?? "http://localhost:4317");
        }));
```

---

## 4. Custom Spans

### Manual Span Creation

```csharp
// Services/AlbumService.cs
using System.Diagnostics;

public class AlbumService : IAlbumService
{
    private static readonly ActivitySource ActivitySource =
        new("ModularMonolith.Api.AlbumService");

    private readonly IAlbumRepository _repository;
    private readonly ICacheFacade _cache;

    public AlbumService(IAlbumRepository repository, ICacheFacade cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<AlbumModel?> GetByIdAsync(int id, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("GetAlbumById");
        activity?.SetTag("album.id", id);

        // Check cache
        using (var cacheActivity = ActivitySource.StartActivity("CheckCache"))
        {
            cacheActivity?.SetTag("cache.key", $"album:{id}");

            var cached = await _cache.GetAsync<AlbumModel>($"album:{id}", ct);
            if (cached is not null)
            {
                cacheActivity?.SetTag("cache.hit", true);
                activity?.SetTag("cache.hit", true);
                return cached;
            }
            cacheActivity?.SetTag("cache.hit", false);
        }

        activity?.SetTag("cache.hit", false);

        // Query database
        using (var dbActivity = ActivitySource.StartActivity("QueryDatabase"))
        {
            var album = await _repository.GetById(id);
            dbActivity?.SetTag("db.found", album is not null);

            if (album is not null)
            {
                // Cache the result
                await _cache.SetAsync(
                    $"album:{id}",
                    album,
                    new CacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                    },
                    ct);
            }

            return album;
        }
    }
}

// Register the ActivitySource
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("ModularMonolith.Api.AlbumService")
        // ... other configuration
    );
```

### Using Activity Events

```csharp
public async Task<Order> CreateOrderAsync(OrderRequest request, CancellationToken ct)
{
    using var activity = ActivitySource.StartActivity("CreateOrder");
    activity?.SetTag("customer.id", request.CustomerId);
    activity?.SetTag("item.count", request.Items.Count);

    // Add event for validation
    activity?.AddEvent(new ActivityEvent("Validating order"));

    var validationResult = await _validator.ValidateAsync(request, ct);
    if (!validationResult.IsValid)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "Validation failed");
        activity?.SetTag("validation.errors", validationResult.Errors.Count);
        throw new ValidationException(validationResult.Errors);
    }

    activity?.AddEvent(new ActivityEvent("Order validated"));

    // Process order
    activity?.AddEvent(new ActivityEvent("Processing order"));

    var order = await _repository.CreateAsync(request, ct);

    activity?.SetTag("order.id", order.Id);
    activity?.SetTag("order.total", order.Total);
    activity?.AddEvent(new ActivityEvent("Order created", tags: new ActivityTagsCollection
    {
        { "order.id", order.Id }
    }));

    return order;
}
```

---

## 5. Custom Metrics

### Define Metrics

```csharp
// Metrics/ApiMetrics.cs
using System.Diagnostics.Metrics;

public class ApiMetrics
{
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly UpDownCounter<long> _activeRequests;
    private readonly ObservableGauge<int> _queueSize;

    public ApiMetrics(IMeterFactory meterFactory, IBackgroundTaskQueue? queue = null)
    {
        var meter = meterFactory.Create("ModularMonolith.Api");

        _requestCounter = meter.CreateCounter<long>(
            "api.requests.total",
            unit: "requests",
            description: "Total number of API requests");

        _requestDuration = meter.CreateHistogram<double>(
            "api.request.duration",
            unit: "ms",
            description: "Request duration in milliseconds");

        _activeRequests = meter.CreateUpDownCounter<long>(
            "api.requests.active",
            unit: "requests",
            description: "Number of active requests");

        if (queue is BackgroundTaskQueue btq)
        {
            _queueSize = meter.CreateObservableGauge(
                "api.queue.size",
                () => btq.Count,
                unit: "items",
                description: "Number of items in background queue");
        }
    }

    public void RecordRequest(string endpoint, string method, int statusCode)
    {
        _requestCounter.Add(1, new KeyValuePair<string, object?>[]
        {
            new("endpoint", endpoint),
            new("method", method),
            new("status_code", statusCode)
        });
    }

    public void RecordDuration(string endpoint, double durationMs)
    {
        _requestDuration.Record(durationMs, new KeyValuePair<string, object?>[]
        {
            new("endpoint", endpoint)
        });
    }

    public void RequestStarted() => _activeRequests.Add(1);
    public void RequestCompleted() => _activeRequests.Add(-1);
}

// Registration
builder.Services.AddSingleton<ApiMetrics>();
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("ModularMonolith.Api")
        // ... other configuration
    );
```

### Use in Middleware

```csharp
// Middleware/MetricsMiddleware.cs
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiMetrics _metrics;

    public MetricsMiddleware(RequestDelegate next, ApiMetrics metrics)
    {
        _next = next;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        _metrics.RequestStarted();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RequestCompleted();

            var endpoint = context.GetEndpoint()?.DisplayName ?? "unknown";
            _metrics.RecordRequest(endpoint, context.Request.Method, context.Response.StatusCode);
            _metrics.RecordDuration(endpoint, stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}

// Registration
app.UseMiddleware<MetricsMiddleware>();
```

---

## 6. Baggage and Context Propagation

### Setting Baggage

```csharp
// Add baggage that propagates across services
app.Use(async (context, next) =>
{
    var tenantId = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
    if (!string.IsNullOrEmpty(tenantId))
    {
        Baggage.SetBaggage("tenant.id", tenantId);
    }

    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!string.IsNullOrEmpty(userId))
    {
        Baggage.SetBaggage("user.id", userId);
    }

    await next();
});
```

### Reading Baggage

```csharp
// In any service, read propagated baggage
public async Task ProcessAsync()
{
    var tenantId = Baggage.GetBaggage("tenant.id");
    var userId = Baggage.GetBaggage("user.id");

    using var activity = ActivitySource.StartActivity("Process");
    activity?.SetTag("tenant.id", tenantId);
    activity?.SetTag("user.id", userId);

    // Process...
}
```

---

## 7. HTTP Client Instrumentation

### Configure HttpClient

```csharp
// Automatic instrumentation for outgoing HTTP calls
builder.Services.AddHttpClient("ExternalApi", client =>
{
    client.BaseAddress = new Uri("https://api.external.com");
})
.AddStandardResilienceHandler();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation(options =>
        {
            options.FilterHttpRequestMessage = request =>
            {
                // Don't trace health check calls
                return !request.RequestUri?.PathAndQuery.Contains("/health") ?? true;
            };
            options.EnrichWithHttpRequestMessage = (activity, request) =>
            {
                activity.SetTag("http.request.body_size",
                    request.Content?.Headers.ContentLength ?? 0);
            };
            options.EnrichWithHttpResponseMessage = (activity, response) =>
            {
                activity.SetTag("http.response.body_size",
                    response.Content?.Headers.ContentLength ?? 0);
            };
        }));
```

---

## 8. Database Instrumentation

### Entity Framework Core

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
            options.EnrichWithIDbCommand = (activity, command) =>
            {
                activity.SetTag("db.rows_affected", command.Parameters.Count);
            };
        }));
```

### Custom Database Span

```csharp
public async Task<IEnumerable<Album>> GetAllAsync(CancellationToken ct)
{
    using var activity = ActivitySource.StartActivity(
        "AlbumRepository.GetAll",
        ActivityKind.Client);

    activity?.SetTag("db.system", "sqlite");
    activity?.SetTag("db.name", "chinook");
    activity?.SetTag("db.operation", "SELECT");
    activity?.SetTag("db.sql.table", "Album");

    var stopwatch = Stopwatch.StartNew();

    try
    {
        var albums = await _context.Albums
            .Include(a => a.Artist)
            .ToListAsync(ct);

        stopwatch.Stop();
        activity?.SetTag("db.rows_returned", albums.Count);
        activity?.SetTag("db.duration_ms", stopwatch.ElapsedMilliseconds);

        return albums;
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

---

## 9. Sampling Strategies

### Configure Sampling

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        // Sample 10% of traces in production
        .SetSampler(new TraceIdRatioBasedSampler(
            builder.Environment.IsProduction() ? 0.1 : 1.0))

        // Or use parent-based sampling
        .SetSampler(new ParentBasedSampler(
            new TraceIdRatioBasedSampler(0.1)))

        // ... rest of configuration
    );
```

### Custom Sampler

```csharp
// Samplers/ImportantOperationSampler.cs
public class ImportantOperationSampler : Sampler
{
    private readonly Sampler _rootSampler;
    private readonly HashSet<string> _alwaysSampleOperations;

    public ImportantOperationSampler(double ratio)
    {
        _rootSampler = new TraceIdRatioBasedSampler(ratio);
        _alwaysSampleOperations = new HashSet<string>
        {
            "CreateOrder",
            "ProcessPayment",
            "CreateUser"
        };
    }

    public override SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        // Always sample important operations
        if (_alwaysSampleOperations.Contains(parameters.Name))
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }

        // Always sample errors
        if (parameters.Tags?.Any(t => t.Key == "error" && t.Value is true) == true)
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }

        // Use ratio-based sampling for everything else
        return _rootSampler.ShouldSample(parameters);
    }
}
```

---

## 10. Exporting to Multiple Backends

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("ModularMonolith.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("ModularMonolith.Api.*");

        // Export to Jaeger
        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://jaeger:4317");
            options.Protocol = OtlpExportProtocol.Grpc;
        });

        // Also export to console in development
        if (builder.Environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }

        // Export to Azure Monitor
        if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:ConnectionString"]))
        {
            tracing.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString =
                    builder.Configuration["ApplicationInsights:ConnectionString"];
            });
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("ModularMonolith.Api");

        metrics.AddOtlpExporter();

        if (builder.Environment.IsDevelopment())
        {
            metrics.AddConsoleExporter();
        }

        // Export to Prometheus
        metrics.AddPrometheusExporter();
    });

// Prometheus endpoint
app.MapPrometheusScrapingEndpoint();
```

---

## 11. Correlating Logs with Traces

```csharp
// Serilog integration
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.With<ActivityEnricher>(); // Add trace context to logs
});

// Custom enricher
public class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("ParentSpanId", activity.ParentSpanId.ToString()));
    }
}
```

---

## 12. Complete Configuration

```csharp
// Program.cs
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

// Service name for all telemetry
var serviceName = "ModularMonolith.Api";
var serviceVersion = "1.0.0";

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName, serviceVersion: serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["host.name"] = Environment.MachineName
        }))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.Filter = context =>
                    !context.Request.Path.StartsWithSegments("/health");
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
            })
            .AddSource("ModularMonolith.Api.*");

        if (builder.Environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }

        var otlpEndpoint = builder.Configuration["Otel:Endpoint"];
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddMeter("ModularMonolith.Api");

        if (builder.Environment.IsDevelopment())
        {
            metrics.AddConsoleExporter();
        }

        var otlpEndpoint = builder.Configuration["Otel:Endpoint"];
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            metrics.AddOtlpExporter();
        }
    });

// Add custom metrics
builder.Services.AddSingleton<ApiMetrics>();

var app = builder.Build();

// Metrics endpoint for Prometheus
app.MapPrometheusScrapingEndpoint();

app.Run();
```

---

## Summary

### OpenTelemetry Components

| Component | Purpose |
|-----------|---------|
| Traces | Distributed request tracking |
| Metrics | Aggregate measurements |
| Baggage | Cross-service context |
| Exporters | Send data to backends |

### Best Practices

1. **Use automatic instrumentation** — Start with built-in instrumentations
2. **Add custom spans for business logic** — Important operations deserve tracking
3. **Include meaningful attributes** — IDs, counts, statuses
4. **Sample appropriately** — 100% sampling can be expensive in production
5. **Correlate with logs** — Include trace IDs in log messages
6. **Monitor metrics dashboards** — Set up alerts on key metrics

### Useful Exporters

| Backend | Exporter Package |
|---------|-----------------|
| Jaeger | `OpenTelemetry.Exporter.OpenTelemetryProtocol` |
| Zipkin | `OpenTelemetry.Exporter.Zipkin` |
| Azure Monitor | `Azure.Monitor.OpenTelemetry.Exporter` |
| Prometheus | `OpenTelemetry.Exporter.Prometheus.AspNetCore` |
| Console | `OpenTelemetry.Exporter.Console` |
