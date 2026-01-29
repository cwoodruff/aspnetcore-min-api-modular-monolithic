# Endpoint Filters Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

Endpoint filters are the Minimal API equivalent of MVC action filters. They
allow you to run code before and after endpoint execution, making them perfect
for cross-cutting concerns like validation, logging, and authorization.

**Duration:** 45-60 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of middleware

---

## Learning Objectives

By the end of this guide, you will:

- Understand the endpoint filter pipeline
- Create custom endpoint filters
- Chain multiple filters together
- Use filters for validation, logging, and authorization
- Know when to use filters vs. middleware

---

## 1. Understanding Endpoint Filters

### Filter Pipeline

```
Request → Middleware → Routing → [Endpoint Filters] → Endpoint Handler → [Endpoint Filters] → Response
                                      ↑                                         ↑
                                   Before                                    After
```

### Filter vs. Middleware

| Aspect                       | Middleware   | Endpoint Filter    |
|------------------------------|--------------|--------------------|
| Scope                        | All requests | Specific endpoints |
| Access to endpoint metadata  | No           | Yes                |
| Access to handler parameters | No           | Yes                |
| Can short-circuit            | Yes          | Yes                |
| Order                        | Global       | Per-endpoint       |

---

## 2. Basic Filter Implementation

### Inline Filter

```csharp
app.MapGet("/albums/{id}", async (int id, IAlbumService service) =>
{
    var album = await service.GetByIdAsync(id);
    return album is not null ? Results.Ok(album) : Results.NotFound();
})
.AddEndpointFilter(async (context, next) =>
{
    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Before endpoint execution");

    var result = await next(context);

    logger.LogInformation("After endpoint execution");

    return result;
});
```

### Class-Based Filter

```csharp
// Filters/LoggingEndpointFilter.cs
public class LoggingEndpointFilter : IEndpointFilter
{
    private readonly ILogger<LoggingEndpointFilter> _logger;

    public LoggingEndpointFilter(ILogger<LoggingEndpointFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var endpointName = endpoint?.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName
            ?? "Unknown";

        _logger.LogInformation("Executing endpoint: {EndpointName}", endpointName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await next(context);

            stopwatch.Stop();
            _logger.LogInformation(
                "Endpoint {EndpointName} completed in {ElapsedMs}ms",
                endpointName,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Endpoint {EndpointName} failed after {ElapsedMs}ms",
                endpointName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

// Usage
app.MapGet("/albums/{id}", GetAlbumById)
   .AddEndpointFilter<LoggingEndpointFilter>();
```

---

## 3. Validation Filter

### Generic Validation Filter

```csharp
// Filters/ValidationFilter.cs
using FluentValidation;

public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Find the argument of type T
        var argument = context.Arguments
            .FirstOrDefault(a => a?.GetType() == typeof(T)) as T;

        if (argument is null)
        {
            return await next(context);
        }

        // Get validator from DI
        var validator = context.HttpContext.RequestServices
            .GetService<IValidator<T>>();

        if (validator is null)
        {
            return await next(context);
        }

        // Validate
        var validationResult = await validator.ValidateAsync(argument);

        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(
                validationResult.ToDictionary(),
                title: "Validation Failed",
                detail: "One or more validation errors occurred.");
        }

        return await next(context);
    }
}

// Usage
app.MapPost("/albums", async (AlbumCreateRequest request, IAlbumService service) =>
{
    var album = await service.CreateAsync(request);
    return Results.Created($"/albums/{album.Id}", album);
})
.AddEndpointFilter<ValidationFilter<AlbumCreateRequest>>();
```

### Auto-Validation Filter Factory

```csharp
// Filters/ValidationFilterFactory.cs
public static class ValidationFilterFactory
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        where T : class
    {
        return builder.AddEndpointFilter<ValidationFilter<T>>();
    }
}

// Usage - cleaner syntax
app.MapPost("/albums", CreateAlbum)
   .WithValidation<AlbumCreateRequest>();

app.MapPut("/albums/{id}", UpdateAlbum)
   .WithValidation<AlbumUpdateRequest>();
```

---

## 4. Audit Logging Filter

```csharp
// Filters/AuditLogFilter.cs
public class AuditLogFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<AuditLogFilter>>();

        // Extract user information
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var userName = httpContext.User.FindFirst(ClaimTypes.Name)?.Value ?? "anonymous";

        // Extract request details
        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path;
        var queryString = httpContext.Request.QueryString;
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Log before execution
        logger.LogInformation(
            "AUDIT: User {UserId} ({UserName}) from {ClientIp} - {Method} {Path}{Query}",
            userId, userName, clientIp, method, path, queryString);

        var result = await next(context);

        // Log result status
        var statusCode = result switch
        {
            IStatusCodeHttpResult statusResult => statusResult.StatusCode,
            _ => 200
        };

        logger.LogInformation(
            "AUDIT: User {UserId} - {Method} {Path} completed with status {StatusCode}",
            userId, method, path, statusCode);

        return result;
    }
}

// Attribute-style marker for audited endpoints
[AttributeUsage(AttributeTargets.Method)]
public class AuditedAttribute : Attribute { }

// Filter that only runs on audited endpoints
public class ConditionalAuditFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var isAudited = endpoint?.Metadata.GetMetadata<AuditedAttribute>() is not null;

        if (!isAudited)
        {
            return await next(context);
        }

        // Perform audit logging...
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<ConditionalAuditFilter>>();

        logger.LogInformation("Audited endpoint accessed");

        return await next(context);
    }
}
```

---

## 5. Authorization Enhancement Filter

```csharp
// Filters/TenantAuthorizationFilter.cs
public class TenantAuthorizationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Get tenant from route or header
        var tenantId = httpContext.Request.RouteValues["tenantId"]?.ToString()
            ?? httpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault();

        if (string.IsNullOrEmpty(tenantId))
        {
            return Results.Problem(
                title: "Tenant Required",
                detail: "A tenant ID must be provided.",
                statusCode: 400);
        }

        // Get user's allowed tenants from claims
        var userTenants = httpContext.User
            .FindAll("tenant")
            .Select(c => c.Value)
            .ToHashSet();

        if (!userTenants.Contains(tenantId))
        {
            return Results.Problem(
                title: "Forbidden",
                detail: $"You do not have access to tenant {tenantId}.",
                statusCode: 403);
        }

        // Store tenant in HttpContext for downstream use
        httpContext.Items["CurrentTenant"] = tenantId;

        return await next(context);
    }
}

// Usage
app.MapGet("/tenants/{tenantId}/data", GetTenantData)
   .AddEndpointFilter<TenantAuthorizationFilter>()
   .RequireAuthorization();
```

---

## 6. Rate Limiting Filter (Custom)

```csharp
// Filters/CustomRateLimitFilter.cs
public class CustomRateLimitFilter : IEndpointFilter
{
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _entries = new();

    private readonly int _maxRequests;
    private readonly TimeSpan _window;

    public CustomRateLimitFilter(int maxRequests = 10, int windowSeconds = 60)
    {
        _maxRequests = maxRequests;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Create rate limit key (by user or IP)
        var key = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous"
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var entry = _entries.GetOrAdd(key, _ => new RateLimitEntry());

        lock (entry)
        {
            var now = DateTime.UtcNow;

            // Reset window if expired
            if (now - entry.WindowStart > _window)
            {
                entry.WindowStart = now;
                entry.RequestCount = 0;
            }

            entry.RequestCount++;

            if (entry.RequestCount > _maxRequests)
            {
                var retryAfter = entry.WindowStart + _window - now;

                httpContext.Response.Headers["Retry-After"] =
                    ((int)retryAfter.TotalSeconds).ToString();

                return Results.Problem(
                    title: "Too Many Requests",
                    detail: $"Rate limit exceeded. Try again in {(int)retryAfter.TotalSeconds} seconds.",
                    statusCode: 429);
            }
        }

        return await next(context);
    }

    private class RateLimitEntry
    {
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;
        public int RequestCount { get; set; }
    }
}

// Factory for configurable rate limits
public static class RateLimitFilterExtensions
{
    public static RouteHandlerBuilder WithRateLimit(
        this RouteHandlerBuilder builder,
        int maxRequests = 10,
        int windowSeconds = 60)
    {
        return builder.AddEndpointFilter(
            new CustomRateLimitFilter(maxRequests, windowSeconds));
    }
}

// Usage
app.MapPost("/api/expensive-operation", ExpensiveOperation)
   .WithRateLimit(maxRequests: 5, windowSeconds: 300);
```

---

## 7. Caching Filter

```csharp
// Filters/ResponseCacheFilter.cs
public class ResponseCacheFilter : IEndpointFilter
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _duration;

    public ResponseCacheFilter(IMemoryCache cache, TimeSpan? duration = null)
    {
        _cache = cache;
        _duration = duration ?? TimeSpan.FromMinutes(5);
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Only cache GET requests
        if (context.HttpContext.Request.Method != HttpMethods.Get)
        {
            return await next(context);
        }

        // Build cache key from request path and query
        var request = context.HttpContext.Request;
        var cacheKey = $"endpoint:{request.Path}{request.QueryString}";

        // Try to get cached response
        if (_cache.TryGetValue(cacheKey, out CachedResponse? cached))
        {
            context.HttpContext.Response.Headers["X-Cache"] = "HIT";
            return cached!.Result;
        }

        // Execute endpoint
        var result = await next(context);

        // Cache successful responses
        if (result is IStatusCodeHttpResult { StatusCode: 200 } or IValueHttpResult)
        {
            _cache.Set(cacheKey, new CachedResponse(result), _duration);
            context.HttpContext.Response.Headers["X-Cache"] = "MISS";
        }

        return result;
    }

    private record CachedResponse(object? Result);
}
```

---

## 8. Filter Chaining and Order

```csharp
// Filters execute in order they are added (outside-in, then inside-out)
app.MapPost("/albums", CreateAlbum)
   .AddEndpointFilter<LoggingEndpointFilter>()      // 1st before, 4th after
   .AddEndpointFilter<AuditLogFilter>()             // 2nd before, 3rd after
   .AddEndpointFilter<ValidationFilter<Album>>()    // 3rd before, 2nd after
   .AddEndpointFilter<TenantAuthorizationFilter>(); // 4th before, 1st after

// Execution order:
// Request → Logging → Audit → Validation → TenantAuth → Handler
// Response ← Logging ← Audit ← Validation ← TenantAuth ← Handler
```

### Filter Factory for Dependency Injection

```csharp
// Filters/EndpointFilterFactory.cs
public class EndpointFilterFactory<TFilter> : IEndpointFilter
    where TFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var filter = context.HttpContext.RequestServices
            .GetRequiredService<TFilter>();

        return await filter.InvokeAsync(context, next);
    }
}

// Extension method for cleaner registration
public static class FilterExtensions
{
    public static RouteHandlerBuilder AddEndpointFilterWithDI<TFilter>(
        this RouteHandlerBuilder builder)
        where TFilter : class, IEndpointFilter
    {
        return builder.AddEndpointFilter<EndpointFilterFactory<TFilter>>();
    }
}

// Registration
builder.Services.AddScoped<LoggingEndpointFilter>();
builder.Services.AddScoped<AuditLogFilter>();

// Usage
app.MapGet("/albums", GetAlbums)
   .AddEndpointFilterWithDI<LoggingEndpointFilter>();
```

---

## 9. Group-Level Filters

```csharp
// Apply filters to all endpoints in a group
var albumsGroup = app.MapGroup("/api/albums")
    .AddEndpointFilter<LoggingEndpointFilter>()
    .AddEndpointFilter<AuditLogFilter>()
    .RequireAuthorization();

albumsGroup.MapGet("/", GetAllAlbums);
albumsGroup.MapGet("/{id}", GetAlbumById);
albumsGroup.MapPost("/", CreateAlbum)
    .AddEndpointFilter<ValidationFilter<AlbumCreateRequest>>(); // Additional filter
albumsGroup.MapPut("/{id}", UpdateAlbum)
    .AddEndpointFilter<ValidationFilter<AlbumUpdateRequest>>();
albumsGroup.MapDelete("/{id}", DeleteAlbum);
```

---

## 10. Testing Endpoint Filters

```csharp
// Tests/Filters/ValidationFilterTests.cs
public class ValidationFilterTests
{
    [Fact]
    public async Task ValidationFilter_ReturnsValidationProblem_WhenInvalid()
    {
        // Arrange
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var invalidRequest = new AlbumCreateRequest
        {
            Title = "", // Invalid - required
            ArtistId = 0 // Invalid - must be positive
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/albums", invalidRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content
            .ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("Title", problem.Errors.Keys);
        Assert.Contains("ArtistId", problem.Errors.Keys);
    }

    [Fact]
    public async Task ValidationFilter_PassesThrough_WhenValid()
    {
        // Arrange
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var validRequest = new AlbumCreateRequest
        {
            Title = "Valid Album",
            ArtistId = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/albums", validRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

// Unit testing a filter directly
public class LoggingEndpointFilterTests
{
    [Fact]
    public async Task InvokeAsync_LogsBeforeAndAfter()
    {
        // Arrange
        var logger = new Mock<ILogger<LoggingEndpointFilter>>();
        var filter = new LoggingEndpointFilter(logger.Object);

        var httpContext = new DefaultHttpContext();
        var context = new EndpointFilterInvocationContext(httpContext);

        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        // Act
        await filter.InvokeAsync(context, next);

        // Assert
        Assert.True(nextCalled);
        // Verify logging calls...
    }
}
```

---

## Summary

### When to Use Endpoint Filters

| Use Case                  | Filter Type                 |
|---------------------------|-----------------------------|
| Input validation          | `ValidationFilter<T>`       |
| Audit logging             | `AuditLogFilter`            |
| Custom authorization      | `TenantAuthorizationFilter` |
| Performance monitoring    | `LoggingEndpointFilter`     |
| Rate limiting (custom)    | `CustomRateLimitFilter`     |
| Response caching (custom) | `ResponseCacheFilter`       |

### Best Practices

1. **Keep filters focused** — One responsibility per filter
2. **Use DI** — Inject services through the filter factory pattern
3. **Order matters** — Outermost filters run first and last
4. **Group common filters** — Apply to MapGroup for DRY code
5. **Test filters** — Both integration and unit tests
6. **Short-circuit early** — Return Results before `next()` to skip handler

### Files to Create

```
src/
├── Shared/
│   └── SharedKernel/
│       └── Filters/
│           ├── ValidationFilter.cs
│           ├── LoggingEndpointFilter.cs
│           ├── AuditLogFilter.cs
│           └── FilterExtensions.cs
```
