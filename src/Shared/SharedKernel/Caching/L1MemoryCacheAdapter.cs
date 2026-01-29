using Microsoft.Extensions.Caching.Memory;

namespace SharedKernel.Caching;

internal sealed class L1MemoryCacheAdapter(IMemoryCache cache) : IL1Cache
{
    private readonly IMemoryCache _cache = cache;

    public Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken ct)
    {
        var entryOptions = new MemoryCacheEntryOptions();
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            entryOptions.SetAbsoluteExpiration(options.AbsoluteExpirationRelativeToNow.Value);
        }

        if (options.SlidingExpiration.HasValue)
        {
            entryOptions.SetSlidingExpiration(options.SlidingExpiration.Value);
        }

        // Tagging not supported natively in IMemoryCache; best-effort only in composite
        _cache.Set(key, value, entryOptions);
        return Task.CompletedTask;
    }

    public Task<(bool hit, T? value)> TryGetAsync<T>(string key, CancellationToken ct)
    {
        if (_cache.TryGetValue<T>(key, out var value))
        {
            return Task.FromResult((true, value));
        }

        return Task.FromResult((false, default(T)));
    }

    public Task RemoveAsync(string key, CancellationToken ct)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}

internal interface IL1Cache
{
    Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken ct);
    Task<(bool hit, T? value)> TryGetAsync<T>(string key, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}
