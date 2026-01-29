# Output Caching Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

Output Caching caches HTTP responses at the server level, reducing load on your
application by serving cached responses directly. This complements service-level
caching by operating at the HTTP layer.

**Duration:** 30-45 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of HTTP caching

---

## Learning Objectives

By the end of this guide, you will:

- Understand the difference between output caching and service caching
- Configure output caching middleware
- Create custom caching policies
- Implement cache invalidation with tags
- Handle cache variations (by query, header, user)

---

## 1. Output Caching vs Service Caching

### Comparison

| Aspect        | Output Caching                   | Service Caching                |
|---------------|----------------------------------|--------------------------------|
| Layer         | HTTP response                    | Business logic                 |
| What's cached | Entire response (headers + body) | Data objects                   |
| Cache key     | URL + variations                 | Custom keys                    |
| Invalidation  | By tag or path                   | By key or tag                  |
| Best for      | Read-heavy public endpoints      | Complex queries, computed data |
| Transparency  | Response headers indicate cache  | Hidden from client             |

### When to Use Each

```
┌─────────────────────────────────────────────────────────────────┐
│                         Request                                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Output Cache (HTTP Layer)                     │
│  • Caches entire response                                        │
│  • Returns 200/304 without hitting endpoint                      │
│  • Good for: static content, public data, GET requests           │
└─────────────────────────────────────────────────────────────────┘
                              │ (cache miss)
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Endpoint Handler                            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                 Service Cache (Application Layer)                │
│  • Caches data objects                                           │
│  • Reduces database calls                                        │
│  • Good for: complex queries, shared data, expensive operations  │
└─────────────────────────────────────────────────────────────────┘
                              │ (cache miss)
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        Database                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. Basic Configuration

### Add Output Caching Services

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add output caching with default options
builder.Services.AddOutputCache();

var app = builder.Build();

// Add output caching middleware (must be before routing)
app.UseOutputCache();

app.MapGet("/api/genres", async (IGenreService service) =>
{
    return await service.GetAllAsync();
})
.CacheOutput(); // Enable caching with defaults

app.Run();
```

### Default Behavior

- Caches GET and HEAD requests only
- Default expiration: no expiration (until memory pressure)
- Varies by: path and query string
- Does not cache authenticated responses by default

---

## 3. Configuring Cache Duration

### Inline Duration

```csharp
app.MapGet("/api/genres", GetGenres)
    .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(10)));
```

### Named Policies

```csharp
// Program.cs - Define policies
builder.Services.AddOutputCache(options =>
{
    // Short cache for frequently changing data
    options.AddPolicy("Short", policy =>
        policy.Expire(TimeSpan.FromSeconds(30)));

    // Medium cache for semi-static data
    options.AddPolicy("Medium", policy =>
        policy.Expire(TimeSpan.FromMinutes(5)));

    // Long cache for static data
    options.AddPolicy("Long", policy =>
        policy.Expire(TimeSpan.FromHours(1)));

    // Default policy
    options.AddBasePolicy(policy =>
        policy.Expire(TimeSpan.FromMinutes(1)));
});

// Use named policies
app.MapGet("/api/genres", GetGenres)
    .CacheOutput("Long");

app.MapGet("/api/albums", GetAlbums)
    .CacheOutput("Medium");

app.MapGet("/api/tracks/popular", GetPopularTracks)
    .CacheOutput("Short");
```

---

## 4. Cache Tags for Invalidation

### Tagging Cached Responses

```csharp
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("GenresCache", policy => policy
        .Expire(TimeSpan.FromMinutes(30))
        .Tag("genres"));

    options.AddPolicy("AlbumsCache", policy => policy
        .Expire(TimeSpan.FromMinutes(20))
        .Tag("albums"));

    options.AddPolicy("ArtistsCache", policy => policy
        .Expire(TimeSpan.FromMinutes(20))
        .Tag("artists"));
});

// Apply policies
app.MapGet("/api/genres", GetGenres).CacheOutput("GenresCache");
app.MapGet("/api/genres/{id}", GetGenreById).CacheOutput("GenresCache");
app.MapGet("/api/albums", GetAlbums).CacheOutput("AlbumsCache");
app.MapGet("/api/albums/{id}", GetAlbumById).CacheOutput("AlbumsCache");
```

### Invalidating by Tag

```csharp
app.MapPost("/api/genres", async (
    GenreCreateRequest request,
    IGenreService service,
    IOutputCacheStore cache,
    CancellationToken ct) =>
{
    var genre = await service.CreateAsync(request, ct);

    // Invalidate all genre-tagged cache entries
    await cache.EvictByTagAsync("genres", ct);

    return TypedResults.Created($"/api/genres/{genre.Id}", genre);
});

app.MapPut("/api/genres/{id}", async (
    int id,
    GenreUpdateRequest request,
    IGenreService service,
    IOutputCacheStore cache,
    CancellationToken ct) =>
{
    var genre = await service.UpdateAsync(id, request, ct);

    if (genre is null)
        return TypedResults.NotFound();

    // Invalidate genre cache
    await cache.EvictByTagAsync("genres", ct);

    return TypedResults.Ok(genre);
});

app.MapDelete("/api/genres/{id}", async (
    int id,
    IGenreService service,
    IOutputCacheStore cache,
    CancellationToken ct) =>
{
    var deleted = await service.DeleteAsync(id, ct);

    if (!deleted)
        return TypedResults.NotFound();

    // Invalidate genre cache
    await cache.EvictByTagAsync("genres", ct);

    return TypedResults.NoContent();
});
```

---

## 5. Cache Variations

### Vary by Query String

```csharp
builder.Services.AddOutputCache(options =>
{
    // Cache varies by specific query parameters
    options.AddPolicy("AlbumsSearch", policy => policy
        .Expire(TimeSpan.FromMinutes(10))
        .SetVaryByQuery("search", "page", "pageSize", "sortBy")
        .Tag("albums"));
});

app.MapGet("/api/albums", GetAlbums).CacheOutput("AlbumsSearch");

// Different cache entries for:
// /api/albums?page=1&pageSize=20
// /api/albums?page=2&pageSize=20
// /api/albums?search=rock&page=1
```

### Vary by Header

```csharp
builder.Services.AddOutputCache(options =>
{
    // Cache varies by Accept-Language header
    options.AddPolicy("LocalizedContent", policy => policy
        .Expire(TimeSpan.FromMinutes(30))
        .SetVaryByHeader("Accept-Language"));

    // Cache varies by custom header
    options.AddPolicy("TenantSpecific", policy => policy
        .Expire(TimeSpan.FromMinutes(15))
        .SetVaryByHeader("X-Tenant-ID"));
});
```

### Vary by Route Value

```csharp
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("ByCategory", policy => policy
        .Expire(TimeSpan.FromMinutes(20))
        .SetVaryByRouteValue("categoryId"));
});

app.MapGet("/api/categories/{categoryId}/products", GetProductsByCategory)
    .CacheOutput("ByCategory");

// Separate cache entries for:
// /api/categories/1/products
// /api/categories/2/products
```

---

## 6. Authenticated User Caching

### Default: No Caching for Authenticated Requests

```csharp
// By default, authenticated requests are NOT cached
app.MapGet("/api/user/profile", GetUserProfile)
    .RequireAuthorization()
    .CacheOutput(); // This won't cache because user is authenticated
```

### Enable Caching with User Variation

```csharp
builder.Services.AddOutputCache(options =>
{
    // Cache per user
    options.AddPolicy("PerUser", policy => policy
        .Expire(TimeSpan.FromMinutes(5))
        .SetVaryByHeader("Authorization") // Each user gets their own cache
        .AllowLocking(true));

    // Or use a custom vary by
    options.AddPolicy("PerUserCustom", builder =>
    {
        builder.AddPolicy<UserCachePolicy>();
    });
});

// Custom policy that varies by user ID
public class UserCachePolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken ct)
    {
        var userId = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            context.CacheVaryByRules.VaryByValues.Add("user", userId);
            context.EnableOutputCaching = true;
            context.AllowCacheLookup = true;
            context.AllowCacheStorage = true;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken ct)
    {
        context.Tags.Add("user-data");
        return ValueTask.CompletedTask;
    }
}

app.MapGet("/api/user/dashboard", GetUserDashboard)
    .RequireAuthorization()
    .CacheOutput("PerUserCustom");
```

---

## 7. Custom Cache Policies

### Full Custom Policy

```csharp
// Policies/ApiCachePolicy.cs
public class ApiCachePolicy : IOutputCachePolicy
{
    private readonly TimeSpan _expiration;
    private readonly string[] _tags;

    public ApiCachePolicy(TimeSpan expiration, params string[] tags)
    {
        _expiration = expiration;
        _tags = tags;
    }

    public ValueTask CacheRequestAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        var request = context.HttpContext.Request;

        // Only cache GET requests
        if (!HttpMethods.IsGet(request.Method))
        {
            context.EnableOutputCaching = false;
            return ValueTask.CompletedTask;
        }

        // Don't cache if Authorization header present (unless we want per-user)
        if (request.Headers.ContainsKey("Authorization"))
        {
            context.EnableOutputCaching = false;
            return ValueTask.CompletedTask;
        }

        // Don't cache if Cache-Control: no-cache
        if (request.Headers.CacheControl.Contains("no-cache"))
        {
            context.AllowCacheLookup = false;
        }

        // Enable caching
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;

        // Vary by query string
        context.CacheVaryByRules.QueryKeys = "*";

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        // Add header to indicate cache hit
        context.HttpContext.Response.Headers["X-Cache"] = "HIT";
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeResponseAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        // Set expiration
        context.ResponseExpirationTimeSpan = _expiration;

        // Add tags for invalidation
        foreach (var tag in _tags)
        {
            context.Tags.Add(tag);
        }

        // Add header to indicate cache miss
        if (context.HttpContext.Response.Headers["X-Cache"].Count == 0)
        {
            context.HttpContext.Response.Headers["X-Cache"] = "MISS";
        }

        return ValueTask.CompletedTask;
    }
}

// Registration
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("GenresApi", new ApiCachePolicy(
        TimeSpan.FromMinutes(30),
        "genres", "api"));

    options.AddPolicy("AlbumsApi", new ApiCachePolicy(
        TimeSpan.FromMinutes(20),
        "albums", "api"));
});
```

---

## 8. Cache Locking

Prevent cache stampede (multiple simultaneous requests regenerating the same
cached item):

```csharp
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("ExpensiveQuery", policy => policy
        .Expire(TimeSpan.FromMinutes(10))
        .SetLocking(true) // Only one request generates the cache
        .Tag("expensive"));
});

app.MapGet("/api/reports/sales-summary", async (IReportService service) =>
{
    // This expensive operation will only run once
    // Other requests wait for the first to complete
    return await service.GenerateSalesSummaryAsync();
})
.CacheOutput("ExpensiveQuery");
```

---

## 9. Distributed Output Cache (Redis)

### Configuration

```csharp
// Add Redis distributed cache
builder.Services.AddStackExchangeRedisOutputCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "ModularMonolith:OutputCache:";
});

// Or use SQL Server
builder.Services.AddSqlServerOutputCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("SqlServer");
    options.SchemaName = "cache";
    options.TableName = "OutputCache";
});
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false",
    "SqlServer": "Server=.;Database=CacheDb;Integrated Security=true"
  }
}
```

---

## 10. Programmatic Cache Control

### Skip Caching for Specific Requests

```csharp
app.MapGet("/api/albums", async (
    HttpContext context,
    IAlbumService service,
    bool? refresh) => // Query parameter to force refresh
{
    if (refresh == true)
    {
        // Disable caching for this request
        var cacheFeature = context.Features.Get<IOutputCacheFeature>();
        cacheFeature?.DisableCache();
    }

    return await service.GetAllAsync();
})
.CacheOutput("AlbumsCache");
```

### Invalidate All Cache

```csharp
app.MapPost("/api/admin/cache/clear", async (
    IOutputCacheStore cache,
    CancellationToken ct) =>
{
    // Clear all cached responses
    await cache.EvictByTagAsync("api", ct);

    return TypedResults.Ok(new { message = "Cache cleared" });
})
.RequireAuthorization("admin");
```

### Invalidate Specific Tags

```csharp
// Service that handles cache invalidation
public class CacheInvalidationService
{
    private readonly IOutputCacheStore _outputCache;
    private readonly ICacheFacade _serviceCache;

    public CacheInvalidationService(
        IOutputCacheStore outputCache,
        ICacheFacade serviceCache)
    {
        _outputCache = outputCache;
        _serviceCache = serviceCache;
    }

    public async Task InvalidateGenresAsync(CancellationToken ct = default)
    {
        // Invalidate output cache
        await _outputCache.EvictByTagAsync("genres", ct);

        // Invalidate service cache
        await _serviceCache.RemoveByTagAsync("music:genre", ct);
    }

    public async Task InvalidateAlbumsAsync(CancellationToken ct = default)
    {
        await _outputCache.EvictByTagAsync("albums", ct);
        await _serviceCache.RemoveByTagAsync("music:album", ct);
    }

    public async Task InvalidateAllMusicDataAsync(CancellationToken ct = default)
    {
        await _outputCache.EvictByTagAsync("music", ct);
        await _serviceCache.RemoveByTagAsync("music", ct);
    }
}

// Use in endpoints
app.MapPut("/api/genres/{id}", async (
    int id,
    GenreUpdateRequest request,
    IGenreService service,
    CacheInvalidationService cacheInvalidation,
    CancellationToken ct) =>
{
    var genre = await service.UpdateAsync(id, request, ct);

    if (genre is not null)
    {
        await cacheInvalidation.InvalidateGenresAsync(ct);
    }

    return genre is not null
        ? TypedResults.Ok(genre)
        : TypedResults.NotFound();
});
```

---

## 11. Response Headers

### Cache Headers Added Automatically

```http
HTTP/1.1 200 OK
Content-Type: application/json
Cache-Control: public, max-age=600
Age: 45
X-Cache: HIT
```

### Custom Cache Headers

```csharp
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("WithHeaders", policy => policy
        .Expire(TimeSpan.FromMinutes(10))
        .With(context =>
        {
            // Add custom response headers
            context.HttpContext.Response.Headers["X-Cached-At"] =
                DateTime.UtcNow.ToString("O");
        }));
});
```

---

## 12. Complete Integration Example

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOutputCache(options =>
{
    // Base policy for all cached endpoints
    options.AddBasePolicy(policy => policy
        .Expire(TimeSpan.FromMinutes(1))
        .Tag("api"));

    // Genre endpoints - rarely change
    options.AddPolicy("Genres", policy => policy
        .Expire(TimeSpan.FromHours(1))
        .Tag("genres")
        .Tag("music"));

    // Album endpoints - moderate changes
    options.AddPolicy("Albums", policy => policy
        .Expire(TimeSpan.FromMinutes(15))
        .SetVaryByQuery("artistId", "genreId", "page", "pageSize")
        .Tag("albums")
        .Tag("music"));

    // Search endpoints - vary by query
    options.AddPolicy("Search", policy => policy
        .Expire(TimeSpan.FromMinutes(5))
        .SetVaryByQuery("*")
        .Tag("search"));

    // User-specific data
    options.AddPolicy("UserData", builder =>
    {
        builder.AddPolicy<UserCachePolicy>();
    });
});

builder.Services.AddScoped<CacheInvalidationService>();

var app = builder.Build();

app.UseOutputCache();

// Genre endpoints
var genres = app.MapGroup("/api/genres").WithTags("Genres");

genres.MapGet("/", GetGenres).CacheOutput("Genres");
genres.MapGet("/{id}", GetGenreById).CacheOutput("Genres");
genres.MapPost("/", CreateGenre); // No cache - writes
genres.MapPut("/{id}", UpdateGenre); // No cache - writes
genres.MapDelete("/{id}", DeleteGenre); // No cache - writes

// Album endpoints
var albums = app.MapGroup("/api/albums").WithTags("Albums");

albums.MapGet("/", GetAlbums).CacheOutput("Albums");
albums.MapGet("/{id}", GetAlbumById).CacheOutput("Albums");
albums.MapGet("/search", SearchAlbums).CacheOutput("Search");
albums.MapPost("/", CreateAlbum);
albums.MapPut("/{id}", UpdateAlbum);
albums.MapDelete("/{id}", DeleteAlbum);

app.Run();
```

---

## Summary

### Output Caching Quick Reference

| Configuration                 | Purpose                       |
|-------------------------------|-------------------------------|
| `.Expire(TimeSpan)`           | Set cache duration            |
| `.Tag("name")`                | Add tag for invalidation      |
| `.SetVaryByQuery("param")`    | Vary cache by query parameter |
| `.SetVaryByHeader("header")`  | Vary cache by header          |
| `.SetVaryByRouteValue("key")` | Vary cache by route value     |
| `.SetLocking(true)`           | Prevent cache stampede        |

### Invalidation Patterns

```csharp
// By tag
await cache.EvictByTagAsync("genres", ct);

// By multiple tags
await Task.WhenAll(
    cache.EvictByTagAsync("genres", ct),
    cache.EvictByTagAsync("albums", ct));
```

### Best Practices

1. **Use tags liberally** — Makes invalidation granular
2. **Combine with service caching** — Output cache for HTTP, service cache for
   data
3. **Set appropriate expirations** — Balance freshness vs. performance
4. **Use locking for expensive operations** — Prevent thundering herd
5. **Monitor cache hit rates** — Tune policies based on actual usage
6. **Don't cache sensitive data** — Be careful with authenticated responses
