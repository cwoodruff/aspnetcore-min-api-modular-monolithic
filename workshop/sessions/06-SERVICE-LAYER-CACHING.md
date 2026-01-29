# Session 6: Service Layer with Caching

**Duration:** 60 minutes
**Session Time:** 1:45 PM - 2:45 PM

---

## Overview

This session covers the service layer pattern with integrated caching. You'll
learn the cache-aside pattern, structured cache key composition, and tag-based
cache invalidation for writes.

---

## Learning Objectives

By the end of this session, you will:

- Understand the service layer's role in the architecture
- Implement the cache-aside pattern
- Use structured cache keys with ICacheKeyComposer
- Apply tag-based cache invalidation
- Understand L1/L2 caching tiers

---

## Part 1: Caching Infrastructure (15 minutes)

### 1.1 Cache Facade Interface

**File: `src/Shared/SharedKernel/Caching/ICacheFacade.cs`**

```csharp
namespace SharedKernel.Caching;

public interface ICacheFacade
{
    // Cache-aside helper
    Task<T?> GetOrAddAsync<T>(CacheKey key, Func<CancellationToken, Task<T?>> factory, CacheEntryOptions? options = null, CancellationToken ct = default);

    Task SetAsync<T>(CacheKey key, T value, CacheEntryOptions? options = null, CancellationToken ct = default);

    Task RemoveAsync(CacheKey key, CancellationToken ct = default);

    // Best-effort tag-based bulk removal
    Task RemoveByTagAsync(string tag, CancellationToken ct = default);
}

public sealed record CacheKey(
    string Environment,
    string App,
    string Module,
    string Entity,
    string Version,
    string? Tenant,
    string? Locale,
    string? Feature,
    string Discriminator)
{
    public override string ToString()
    {
        return string.Join(':', new[]
        {
            Environment,
            App,
            Module,
            Entity,
            Version,
            Tenant ?? string.Empty,
            Locale ?? string.Empty,
            Feature ?? string.Empty,
            Discriminator
        });
    }
}

public sealed class CacheEntryOptions
{
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool AllowStaleWhileRevalidate { get; set; }
    public TimeSpan? MaxStale { get; set; }
    public double JitterPercent { get; set; } = 0.1; // ±10%
}
```

### 1.2 Cache Key Composer

**File: `src/Shared/SharedKernel/Caching/CacheKeyComposer.cs`**

```csharp
using Microsoft.Extensions.Configuration;

namespace SharedKernel.Caching;

public interface ICacheKeyComposer
{
    CacheKey Compose(
        string moduleName,
        string entity,
        string version,
        string discriminator,
        string? tenant = null,
        string? locale = null,
        string? feature = null);
}

internal sealed class CacheKeyComposer(IConfiguration config) : ICacheKeyComposer
{
    private readonly string _env = (config["ASPNETCORE_ENVIRONMENT"] ?? config["DOTNET_ENVIRONMENT"] ?? "prod").ToLowerInvariant();
    private readonly string _app = (config["ServiceName"] ?? "mmapi").ToLowerInvariant();

    public CacheKey Compose(string moduleName, string entity, string version, string discriminator, string? tenant = null, string? locale = null, string? feature = null)
    {
        return new CacheKey(
            Environment: _env,
            App: _app,
            Module: moduleName.ToLowerInvariant(),
            Entity: entity.ToLowerInvariant(),
            Version: version.ToLowerInvariant(),
            Tenant: tenant?.ToLowerInvariant(),
            Locale: locale?.ToLowerInvariant(),
            Feature: feature?.ToLowerInvariant(),
            Discriminator: discriminator
        );
    }
}
```

### 1.3 Cache Key Structure

The cache key follows a hierarchical pattern:

```
{env}:{app}:{module}:{entity}:{version}:{tenant}:{locale}:{feature}:{discriminator}
```

Example:

```
development:mmapi:music:album:v1::::by-id:42
```

Benefits:

- **Namespace isolation** - Different environments don't collide
- **Version support** - Can invalidate old cache versions
- **Multi-tenant** - Optional tenant scoping
- **Easy invalidation** - Prefix-based deletion

---

## Part 2: Composite Cache Implementation (10 minutes)

### 2.1 L1/L2 Cache Architecture

**File: `src/Shared/SharedKernel/Caching/CompositeCacheFacade.cs`**

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SharedKernel.Caching;

internal sealed class CompositeCacheFacade(
    IOptions<CacheOptions> options,
    IL1Cache l1,
    ILogger<CompositeCacheFacade> logger,
    IL2Cache? l2 = null)
    : ICacheFacade
{
    private readonly CacheOptions _opts = options.Value;
    private readonly ILogger<CompositeCacheFacade> _logger = logger;

    // Simple per-key async lock to avoid recomputation stampede
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<T?> GetOrAddAsync<T>(CacheKey key, Func<CancellationToken, Task<T?>> factory, CacheEntryOptions? options = null, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return await factory(ct);

        var cacheKey = key.ToString();

        // 1) L1
        var (hit1, v1) = await l1.TryGetAsync<T>(cacheKey, ct);
        if (hit1) return v1;

        // 2) L2
        if (_opts.Tier.Equals("L1L2", StringComparison.OrdinalIgnoreCase) && l2 is not null)
        {
            var (hit2, v2) = await l2.TryGetAsync<T>(cacheKey, ct);
            if (hit2)
            {
                await l1.SetAsync(cacheKey, v2!, EffectiveOptions(options), ct);
                return v2;
            }
        }

        // 3) Factory with single-flight
        var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // Re-check after acquiring the lock
            var (hitAfter, vAfter) = await l1.TryGetAsync<T>(cacheKey, ct);
            if (hitAfter) return vAfter;

            var value = await factory(ct);
            var eff = EffectiveOptions(options);
            if (value is not null)
            {
                if (_opts.Tier.Equals("L1L2", StringComparison.OrdinalIgnoreCase) && l2 is not null)
                    await l2.SetAsync(cacheKey, value, eff, ct);
                await l1.SetAsync(cacheKey, value, eff, ct);
            }
            return value;
        }
        finally
        {
            gate.Release();
            _ = _locks.TryRemove(cacheKey, out _);
        }
    }

    public async Task SetAsync<T>(CacheKey key, T value, CacheEntryOptions? options = null, CancellationToken ct = default)
    {
        if (!_opts.Enabled) return;
        var k = key.ToString();
        var eff = EffectiveOptions(options);
        if (_opts.Tier.Equals("L1L2", StringComparison.OrdinalIgnoreCase) && l2 is not null)
            await l2.SetAsync(k, value, eff, ct);
        await l1.SetAsync(k, value, eff, ct);
    }

    public async Task RemoveAsync(CacheKey key, CancellationToken ct = default)
    {
        var k = key.ToString();
        await l1.RemoveAsync(k, ct);
        if (l2 is not null) await l2.RemoveAsync(k, ct);
    }

    public Task RemoveByTagAsync(string tag, CancellationToken ct = default)
    {
        // No-op placeholder - tagging requires an index
        return Task.CompletedTask;
    }

    private CacheEntryOptions EffectiveOptions(CacheEntryOptions? options)
    {
        var o = options ?? new CacheEntryOptions();
        if (!o.AbsoluteExpirationRelativeToNow.HasValue)
        {
            o.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_opts.DefaultTTLSeconds);
        }
        // Apply jitter to avoid thundering herd
        if (o.AbsoluteExpirationRelativeToNow is { } ttl && ttl > TimeSpan.Zero && o.JitterPercent > 0)
        {
            var rand = Random.Shared.NextDouble();
            var jitter = (rand - 0.5) * 2 * o.JitterPercent;
            var adjusted = TimeSpan.FromMilliseconds(ttl.TotalMilliseconds * (1 + jitter));
            o.AbsoluteExpirationRelativeToNow = adjusted > TimeSpan.Zero ? adjusted : ttl;
        }
        return o;
    }
}
```

### 2.2 Key Features

| Feature           | Purpose                                 |
|-------------------|-----------------------------------------|
| **L1/L2 tiers**   | In-memory (fast) + distributed (shared) |
| **Single-flight** | Prevents cache stampede                 |
| **TTL jitter**    | Prevents synchronized expiration        |
| **Lock per key**  | Fine-grained concurrency                |

---

## Part 3: Service Layer Implementation (25 minutes)

### 3.1 Service Interface

**File: `src/Modules/Music/Music.Module/Services/IAlbumService.cs`**

```csharp
using SharedKernel.Persistence.ApiModels;

namespace Music.Modules.Services;

public interface IAlbumService
{
    Task<AlbumApiModel?> GetAlbumByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllAlbumsAsync(CancellationToken ct);
    Task<IEnumerable<object>> GetAlbumsByArtistIdAsync(int id, CancellationToken ct);
    Task<AlbumApiModel?> CreateAlbumAsync(AlbumApiModel model, CancellationToken ct);
    Task<bool> UpdateAlbumAsync(AlbumApiModel model, CancellationToken ct);
}
```

### 3.2 Complete Service Implementation

**File: `src/Modules/Music/Music.Module/Services/AlbumService.cs`**

```csharp
using FluentValidation;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Music.Modules.Services;

public class AlbumService(
    IAlbumRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<AlbumApiModel> validator) : IAlbumService
{
    private readonly IValidator<AlbumApiModel> _validator = validator;
    private static readonly string[] AlbumTags = ["music:album", "music:album:by-id"];

    public async Task<AlbumApiModel?> GetAlbumByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "album",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<AlbumApiModel?>(key, async _ =>
        {
            try
            {
                return await repository.GetById(id);
            }
            catch
            {
                return null;
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = AlbumTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllAlbumsAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "album",
            version: "v1",
            discriminator: "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                var entities = await repository.GetAll();
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = AlbumTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetAlbumsByArtistIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "album",
            version: "v1",
            discriminator: $"by-artist:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                var entities = await repository.GetByArtistId(id);
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = AlbumTags
        }, ct) ?? [];
    }

    public async Task<AlbumApiModel?> CreateAlbumAsync(AlbumApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var created = await repository.Add(entity);

        // Invalidate cache
        await cache.RemoveByTagAsync(AlbumTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdateAlbumAsync(AlbumApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var updated = await repository.Update(entity);

        if (updated)
        {
            // Invalidate cache
            await cache.RemoveByTagAsync(AlbumTags[0], ct);
            var key = keys.Compose(
                moduleName: "music",
                entity: "album",
                version: "v1",
                discriminator: $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
```

### 3.3 Cache-Aside Pattern Explained

```csharp
return await cache.GetOrAddAsync<AlbumApiModel?>(key, async _ =>
{
    // This factory is called on cache miss
    return await repository.GetById(id);
}, options, ct);
```

Flow:

1. Check cache for key
2. If found (cache hit), return cached value
3. If not found (cache miss), call factory
4. Store factory result in cache
5. Return result

### 3.4 Cache Invalidation on Write

```csharp
public async Task<AlbumApiModel?> CreateAlbumAsync(AlbumApiModel model, CancellationToken ct)
{
    // ... validation and create

    // Invalidate cache by tag
    await cache.RemoveByTagAsync(AlbumTags[0], ct);

    return created?.Convert();
}

public async Task<bool> UpdateAlbumAsync(AlbumApiModel model, CancellationToken ct)
{
    // ... validation and update

    if (updated)
    {
        // Invalidate both tag and specific key
        await cache.RemoveByTagAsync(AlbumTags[0], ct);
        var key = keys.Compose(...);
        await cache.RemoveAsync(key, ct);
    }

    return updated;
}
```

---

## Part 4: Caching Registration (5 minutes)

### 4.1 Central Caching Registration

**File: `src/Shared/SharedKernel/Caching/CachingRegistration.cs`**

```csharp
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Caching;

public static class CachingRegistration
{
    public static IServiceCollection AddCentralCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CacheOptions>().Bind(configuration.GetSection("Caching"));

        // Always register L1 IMemoryCache
        services.AddMemoryCache();
        services.AddSingleton<IL1Cache, L1MemoryCacheAdapter>();

        // Optionally register L2 provider
        var provider = configuration["Caching:Provider"] ?? "InMemory";
        var tier = configuration["Caching:Tier"] ?? "L1";

        if (string.Equals(tier, "L1L2", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                services.AddDistributedMemoryCache();
            }

            services.AddSingleton<IL2Cache>(sp =>
            {
                var dist = sp.GetService<IDistributedCache>();
                return dist is not null ? new L2DistributedCacheAdapter(dist) : null!;
            });
        }

        services.AddSingleton<ICacheKeyComposer, CacheKeyComposer>();
        services.AddSingleton<ICacheFacade>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CacheOptions>>();
            var l1 = sp.GetRequiredService<IL1Cache>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CompositeCacheFacade>>();
            var l2 = sp.GetService<IL2Cache>();
            return new CompositeCacheFacade(opts, l1, logger, l2);
        });

        return services;
    }
}
```

### 4.2 Service Registration in Module

**File: `src/Modules/Music/Music.Module/Module.cs` (partial)**

```csharp
public void RegisterServices(IServiceCollection services, IConfiguration config)
{
    // Register module-specific services
    services.AddScoped<IAlbumService, AlbumService>();
    services.AddScoped<IArtistService, ArtistService>();
    services.AddScoped<IPlaylistService, PlaylistService>();
    services.AddScoped<ITrackService, TrackService>();
}
```

---

## Part 5: Cache Configuration (5 minutes)

### 5.1 appsettings.json

```json
{
  "Caching": {
    "Enabled": true,
    "Tier": "L1",
    "Provider": "InMemory",
    "DefaultTTLSeconds": 300
  }
}
```

### 5.2 Configuration Options

| Option              | Description                                     | Default    |
|---------------------|-------------------------------------------------|------------|
| `Enabled`           | Enable/disable caching globally                 | `true`     |
| `Tier`              | `L1` (memory only) or `L1L2` (with distributed) | `L1`       |
| `Provider`          | `InMemory` or `Redis`                           | `InMemory` |
| `DefaultTTLSeconds` | Default TTL if not specified                    | `300`      |

---

## Checkpoint

Before moving to Session 7, verify:

- [ ] Understand cache-aside pattern
- [ ] Know how to compose cache keys
- [ ] Understand cache invalidation strategies
- [ ] Can implement service layer with caching
- [ ] Understand L1/L2 cache tiers

---

## Quick Reference

### Service Pattern Template

```csharp
public class YourService(
    IYourRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<YourModel> validator) : IYourService
{
    private static readonly string[] Tags = ["module:entity"];

    public async Task<YourModel?> GetByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "module",
            entity: "entity",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<YourModel?>(key, async _ =>
        {
            return await repository.GetById(id);
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = Tags
        }, ct);
    }

    public async Task<YourModel?> CreateAsync(YourModel model, CancellationToken ct)
    {
        // Validate
        var result = await validator.ValidateAsync(model, ct);
        if (!result.IsValid) throw new ValidationException(result.Errors);

        // Create
        var created = await repository.Add(model.ToEntity());

        // Invalidate cache
        await cache.RemoveByTagAsync(Tags[0], ct);

        return created?.ToModel();
    }
}
```

### Cache Key Examples

| Operation   | Discriminator    | Full Key (example)                        |
|-------------|------------------|-------------------------------------------|
| Get by ID   | `by-id:{id}`     | `dev:mmapi:music:album:v1::::by-id:42`    |
| Get all     | `all`            | `dev:mmapi:music:album:v1::::all`         |
| By relation | `by-artist:{id}` | `dev:mmapi:music:album:v1::::by-artist:5` |

---

## Next Session

In **Session 7: FluentValidation**, you will:

- Create validators for API models
- Register validators with DI
- Handle validation errors in services
- Return proper validation problem responses
