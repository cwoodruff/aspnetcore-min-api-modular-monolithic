using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace SharedKernel.Caching;

internal sealed class L2DistributedCacheAdapter(IDistributedCache cache) : IL2Cache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDistributedCache _cache = cache;

    public async Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
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

        var value = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
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
