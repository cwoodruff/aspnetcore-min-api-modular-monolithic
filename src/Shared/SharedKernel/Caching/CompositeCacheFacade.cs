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
    // Simple per-key async lock to avoid recomputation stampede
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ILogger<CompositeCacheFacade> _logger = logger;
    private readonly CacheOptions _opts = options.Value;

    public async Task<T?> GetOrAddAsync<T>(CacheKey key, Func<CancellationToken, Task<T?>> factory,
        CacheEntryOptions? options = null, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
        {
            return await factory(ct);
        }

        var cacheKey = key.ToString();

        // 1) L1
        var (hit1, v1) = await l1.TryGetAsync<T>(cacheKey, ct);
        if (hit1)
        {
            return v1;
        }

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
            if (hitAfter)
            {
                return vAfter;
            }

            var value = await factory(ct);
            var eff = EffectiveOptions(options);
            if (value is not null)
            {
                if (_opts.Tier.Equals("L1L2", StringComparison.OrdinalIgnoreCase) && l2 is not null)
                {
                    await l2.SetAsync(cacheKey, value, eff, ct);
                }

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

    public async Task SetAsync<T>(CacheKey key, T value, CacheEntryOptions? options = null,
        CancellationToken ct = default)
    {
        if (!_opts.Enabled)
        {
            return;
        }

        var k = key.ToString();
        var eff = EffectiveOptions(options);
        if (_opts.Tier.Equals("L1L2", StringComparison.OrdinalIgnoreCase) && l2 is not null)
        {
            await l2.SetAsync(k, value, eff, ct);
        }

        await l1.SetAsync(k, value, eff, ct);
    }

    public async Task RemoveAsync(CacheKey key, CancellationToken ct = default)
    {
        var k = key.ToString();
        await l1.RemoveAsync(k, ct);
        if (l2 is not null)
        {
            await l2.RemoveAsync(k, ct);
        }
    }

    public Task RemoveByTagAsync(string tag, CancellationToken ct = default)
    {
        // Minimal implementation: tagging not supported natively without an index.
        // This is a no-op placeholder to keep the API stable until an L2 tag index is introduced.
        // No-op placeholder to keep the API stable until an L2 tag index is introduced.
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
            var jitter = (rand - 0.5) * 2 * o.JitterPercent; // -p..+p
            var adjusted = TimeSpan.FromMilliseconds(ttl.TotalMilliseconds * (1 + jitter));
            o.AbsoluteExpirationRelativeToNow = adjusted > TimeSpan.Zero ? adjusted : ttl;
        }

        return o;
    }
}
