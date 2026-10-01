using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace SharedKernel.Caching;

/// <remarks>Serializes with the app's <see cref="ModuleJson" /> options, so cached types need type information there.</remarks>
internal sealed class L2DistributedCacheAdapter(IDistributedCache cache, JsonSerializerOptions json) : IL2Cache
{
    private readonly IDistributedCache _cache = cache;

    public async Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, json.TypeInfo<T>());
        var entryOptions = new DistributedCacheEntryOptions();
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            entryOptions.SetAbsoluteExpiration(options.AbsoluteExpirationRelativeToNow.Value);
        }

        if (options.SlidingExpiration.HasValue)
        {
            entryOptions.SetSlidingExpiration(options.SlidingExpiration.Value);
        }

        await _cache.SetAsync(key, bytes, entryOptions, ct);
    }

    public async Task<(bool hit, T? value)> TryGetAsync<T>(string key, CancellationToken ct)
    {
        var bytes = await _cache.GetAsync(key, ct);
        if (bytes is null || bytes.Length == 0)
        {
            return (false, default);
        }

        var value = JsonSerializer.Deserialize(bytes, json.TypeInfo<T>());
        return (true, value);
    }

    public Task RemoveAsync(string key, CancellationToken ct)
    {
        return _cache.RemoveAsync(key, ct);
    }
}

internal interface IL2Cache
{
    Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken ct);
    Task<(bool hit, T? value)> TryGetAsync<T>(string key, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}
