# Structured Logging & Correlation IDs Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

Structured logging captures log data as structured objects rather than plain
text strings, making logs searchable, filterable, and analyzable. Correlation
IDs link related log entries across distributed requests.

**Duration:** 45-60 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of logging
concepts

---

## Learning Objectives

By the end of this guide, you will:

- Implement structured logging with Serilog
- Add correlation IDs to track requests
- Configure log enrichment
- Set up centralized logging
- Create custom log scopes

---

## 1. Structured vs Unstructured Logging

### Unstructured (Traditional)

```csharp
_logger.LogInformation($"User {userId} created order {orderId} for ${amount}");
// Output: "User 123 created order 456 for $99.99"
// Hard to query: How many orders over $100? Which user created most orders?
```

### Structured

```csharp
_logger.LogInformation("User {UserId} created order {OrderId} for {Amount:C}",
    userId, orderId, amount);
// Output (JSON):
// {
//   "Message": "User 123 created order 456 for $99.99",
//   "UserId": 123,
//   "OrderId": 456,
//   "Amount": 99.99,
//   "Timestamp": "2024-01-15T10:30:00Z"
// }
// Easy to query: Filter by UserId, aggregate Amount, etc.
```

---

## 2. Setting Up Serilog

### Install Packages

```xml
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="2.3.0" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="3.1.0" />
<PackageReference Include="Serilog.Enrichers.Process" Version="2.0.2" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="Serilog.Sinks.Seq" Version="6.0.0" />
```

### Basic Configuration

```csharp
// Program.cs
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("Starting application");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ... configure services

    var app = builder.Build();

    // ... configure middleware

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

---

## 3. Configuration via appsettings.json

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File", "Serilog.Sinks.Seq"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
        "System.Net.Http.HttpClient": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"],
    "Properties": {
      "Application": "ModularMonolith.Api"
    }
  }
}
```

```csharp
// Program.cs - Load from configuration
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});
```

---

## 4. Correlation ID Middleware

### Middleware Implementation

```csharp
// Middleware/CorrelationIdMiddleware.cs
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get correlation ID from header or generate new one
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // Add to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Store in HttpContext for access in handlers
        context.Items["CorrelationId"] = correlationId;

        // Add to log context
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}

// Extension method
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}

// Registration in Program.cs
app.UseCorrelationId();
app.UseSerilogRequestLogging();
```

### Accessing Correlation ID in Code

```csharp
// In an endpoint or service
app.MapPost("/api/orders", async (
    OrderRequest request,
    HttpContext context,
    IOrderService orderService,
    ILogger<Program> logger) =>
{
    var correlationId = context.Items["CorrelationId"]?.ToString();

    logger.LogInformation(
        "Processing order request for customer {CustomerId}",
        request.CustomerId);

    // The CorrelationId is automatically included via LogContext
    var order = await orderService.CreateAsync(request);

    return TypedResults.Created($"/api/orders/{order.Id}", order);
});
```

---

## 5. Request Logging Enrichment

### Serilog Request Logging

```csharp
// Program.cs
app.UseSerilogRequestLogging(options =>
{
    // Customize the message template
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";

    // Attach additional properties
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());

        // Add user info if authenticated
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            diagnosticContext.Set("UserId",
                httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            diagnosticContext.Set("UserName",
                httpContext.User.FindFirst(ClaimTypes.Name)?.Value);
        }

        // Add correlation ID
        if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId))
        {
            diagnosticContext.Set("CorrelationId", correlationId);
        }
    };

    // Control which requests to log
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        if (ex != null || httpContext.Response.StatusCode >= 500)
            return LogEventLevel.Error;

        if (httpContext.Response.StatusCode >= 400)
            return LogEventLevel.Warning;

        if (elapsed > 1000) // Slow request
            return LogEventLevel.Warning;

        // Don't log health check endpoints at Information level
        if (httpContext.Request.Path.StartsWithSegments("/health"))
            return LogEventLevel.Debug;

        return LogEventLevel.Information;
    };
});
```

---

## 6. Custom Log Enrichers

### User Enricher

```csharp
// Logging/Enrichers/UserEnricher.cs
public class UserEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        var user = httpContext.User;
        if (user.Identity?.IsAuthenticated != true) return;

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("UserId", userId));
        }

        var tenantId = user.FindFirst("tenant")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TenantId", tenantId));
        }
    }
}

// Registration
builder.Services.AddHttpContextAccessor();
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.With(new UserEnricher(services.GetRequiredService<IHttpContextAccessor>()));
});
```

### Operation Enricher

```csharp
// Logging/Enrichers/OperationEnricher.cs
public class OperationEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OperationEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        var endpoint = httpContext.GetEndpoint();
        var endpointName = endpoint?.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName;

        if (!string.IsNullOrEmpty(endpointName))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("EndpointName", endpointName));
        }

        var routePattern = (endpoint as RouteEndpoint)?.RoutePattern.RawText;
        if (!string.IsNullOrEmpty(routePattern))
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("RoutePattern", routePattern));
        }
    }
}
```

---

## 7. Log Scopes

### Using Log Scopes

```csharp
// Services/OrderService.cs
public class OrderService : IOrderService
{
    private readonly ILogger<OrderService> _logger;
    private readonly IOrderRepository _repository;

    public OrderService(ILogger<OrderService> logger, IOrderRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task<Order> CreateAsync(OrderRequest request, CancellationToken ct)
    {
        // Create a scope that adds properties to all log messages within
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CustomerId"] = request.CustomerId,
            ["ItemCount"] = request.Items.Count,
            ["OrderTotal"] = request.Items.Sum(i => i.Price * i.Quantity)
        });

        _logger.LogInformation("Starting order creation");

        try
        {
            // Validate inventory
            foreach (var item in request.Items)
            {
                using var itemScope = _logger.BeginScope(
                    new Dictionary<string, object> { ["ProductId"] = item.ProductId });

                _logger.LogDebug("Checking inventory for product");
                // ... check inventory
            }

            var order = await _repository.CreateAsync(request, ct);

            _logger.LogInformation("Order {OrderId} created successfully", order.Id);

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order");
            throw;
        }
    }
}
```

### Custom Scope Provider

```csharp
// Logging/OperationScope.cs
public static class OperationScope
{
    public static IDisposable BeginOperation(
        this ILogger logger,
        string operationName,
        params (string Key, object Value)[] properties)
    {
        var dict = new Dictionary<string, object>
        {
            ["Operation"] = operationName,
            ["OperationId"] = Guid.NewGuid().ToString("N")[..8]
        };

        foreach (var (key, value) in properties)
        {
            dict[key] = value;
        }

        return logger.BeginScope(dict);
    }
}

// Usage
using var scope = _logger.BeginOperation("CreateOrder",
    ("CustomerId", customerId),
    ("ItemCount", items.Count));

_logger.LogInformation("Processing order");
```

---

## 8. Sensitive Data Masking

### Custom Destructuring Policy

```csharp
// Logging/SensitiveDataDestructuringPolicy.cs
public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "Secret",
        "Token",
        "ApiKey",
        "ConnectionString",
        "CreditCard",
        "SSN",
        "SocialSecurityNumber"
    };

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue? result)
    {
        result = null;

        if (value is not IDictionary<string, object> dict)
            return false;

        var sanitized = new Dictionary<string, object>();

        foreach (var kvp in dict)
        {
            if (SensitiveProperties.Contains(kvp.Key))
            {
                sanitized[kvp.Key] = "***REDACTED***";
            }
            else
            {
                sanitized[kvp.Key] = kvp.Value;
            }
        }

        result = propertyValueFactory.CreatePropertyValue(sanitized, destructureObjects: true);
        return true;
    }
}

// Registration
Log.Logger = new LoggerConfiguration()
    .Destructure.With<SensitiveDataDestructuringPolicy>()
    // ... rest of configuration
    .CreateLogger();
```

### Property Masking Extension

```csharp
// Logging/LoggerExtensions.cs
public static class LoggerExtensions
{
    public static void LogSensitive(
        this ILogger logger,
        LogLevel level,
        string message,
        params (string Name, object Value, bool IsSensitive)[] properties)
    {
        var sanitizedProps = properties.Select(p =>
            p.IsSensitive
                ? new KeyValuePair<string, object>(p.Name, MaskValue(p.Value))
                : new KeyValuePair<string, object>(p.Name, p.Value));

        using var scope = logger.BeginScope(sanitizedProps);
        logger.Log(level, message);
    }

    private static string MaskValue(object value)
    {
        var str = value?.ToString() ?? "";
        if (str.Length <= 4) return "****";
        return str[..2] + new string('*', str.Length - 4) + str[^2..];
    }
}

// Usage
_logger.LogSensitive(LogLevel.Information, "Processing payment",
    ("CustomerId", customerId, false),
    ("CreditCard", creditCard, true),
    ("Amount", amount, false));
// Logs: Processing payment with CustomerId=123, CreditCard=41**********56, Amount=99.99
```

---

## 9. Centralized Logging with Seq

### Docker Compose for Seq

```yaml
version: '3.8'

services:
  api:
    build: .
    environment:
      - Serilog__WriteTo__2__Args__serverUrl=http://seq:5341
    depends_on:
      - seq

  seq:
    image: datalust/seq:latest
    environment:
      - ACCEPT_EULA=Y
    ports:
      - "5341:5341"  # Ingestion
      - "8081:80"    # UI
    volumes:
      - seq-data:/data

volumes:
  seq-data:
```

### Querying Logs in Seq

```sql
-- Find all errors for a correlation ID
CorrelationId = "abc123" and @Level = "Error"

-- Find slow requests
Elapsed > 1000

-- Find all actions by a user
UserId = "user-456"

-- Count errors by endpoint
select count(*) from stream
where @Level = "Error"
group by EndpointName

-- Find all orders over $100
Operation = "CreateOrder" and OrderTotal > 100
```

---

## 10. Performance Considerations

### Conditional Logging

```csharp
// Avoid expensive operations if log level is disabled
if (_logger.IsEnabled(LogLevel.Debug))
{
    var expensiveData = ComputeExpensiveDebugData();
    _logger.LogDebug("Debug data: {Data}", expensiveData);
}
```

### Source Generators (High Performance)

```csharp
// Using LoggerMessage source generators for high-performance logging
public static partial class LogMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Processing order {OrderId} for customer {CustomerId}")]
    public static partial void ProcessingOrder(
        this ILogger logger,
        int orderId,
        int customerId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Order {OrderId} processing failed")]
    public static partial void OrderProcessingFailed(
        this ILogger logger,
        int orderId,
        Exception exception);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Slow database query detected: {QueryName} took {ElapsedMs}ms")]
    public static partial void SlowQuery(
        this ILogger logger,
        string queryName,
        long elapsedMs);
}

// Usage
_logger.ProcessingOrder(orderId, customerId);
_logger.OrderProcessingFailed(orderId, ex);
_logger.SlowQuery("GetOrdersByCustomer", stopwatch.ElapsedMilliseconds);
```

---

## 11. Exception Logging

### Structured Exception Logging

```csharp
try
{
    await ProcessOrderAsync(order);
}
catch (ValidationException ex)
{
    _logger.LogWarning(ex,
        "Order validation failed for {OrderId}. Errors: {ValidationErrors}",
        order.Id,
        ex.Errors);
    throw;
}
catch (ExternalServiceException ex)
{
    _logger.LogError(ex,
        "External service {ServiceName} failed for order {OrderId}. " +
        "Status: {StatusCode}, Response: {Response}",
        ex.ServiceName,
        order.Id,
        ex.StatusCode,
        ex.ResponseBody);
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex,
        "Unexpected error processing order {OrderId}",
        order.Id);
    throw;
}
```

---

## 12. Complete Implementation

```csharp
// Program.cs
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "ModularMonolith.Api")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting ModularMonolith.Api");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();
    builder.Services.AddHttpContextAccessor();

    // ... other services

    var app = builder.Build();

    // Correlation ID must come before request logging
    app.UseCorrelationId();

    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("ClientIP",
                httpContext.Connection.RemoteIpAddress?.ToString());

            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                diagnosticContext.Set("UserId",
                    httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            }
        };
    });

    // ... other middleware

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
```

---

## Summary

### Key Components

| Component      | Purpose                            |
|----------------|------------------------------------|
| Serilog        | Structured logging library         |
| Correlation ID | Links related log entries          |
| Log Enrichers  | Add context to all logs            |
| Log Scopes     | Add context to specific operations |
| Seq/ELK        | Centralized log aggregation        |

### Best Practices

1. **Use message templates** — Not string interpolation
2. **Include correlation IDs** — Essential for distributed tracing
3. **Enrich with context** — User, tenant, operation
4. **Mask sensitive data** — Passwords, tokens, PII
5. **Use appropriate levels** — Debug for details, Info for flow, Warn/Error for
   problems
6. **Aggregate logs centrally** — Seq, Elasticsearch, Application Insights

### Log Levels Guide

| Level       | Use For                                     |
|-------------|---------------------------------------------|
| Verbose     | Detailed debugging (disabled in production) |
| Debug       | Developer diagnostics                       |
| Information | Normal application flow                     |
| Warning     | Unexpected but recoverable situations       |
| Error       | Failures that need attention                |
| Fatal       | Application crashes                         |
