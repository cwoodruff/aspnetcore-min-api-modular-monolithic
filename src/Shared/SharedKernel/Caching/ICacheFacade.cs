namespace SharedKernel.Caching;

public interface ICacheFacade
{
    // Cache-aside helper
    Task<T?> GetOrAddAsync<T>(CacheKey key, Func<CancellationToken, Task<T?>> factory, CacheEntryOptions? options = null, CancellationToken ct = default);

    Task SetAsync<T>(CacheKey key, T value, CacheEntryOptions? options = null, CancellationToken ct = default);

    Task RemoveAsync(CacheKey key, CancellationToken ct = default);

    // Best-effort tag-based bulk removal (no-op if tagging disabled or unsupported)
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
