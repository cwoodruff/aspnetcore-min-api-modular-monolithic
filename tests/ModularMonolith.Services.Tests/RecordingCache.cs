using NSubstitute;
using SharedKernel.Caching;

namespace ModularMonolith.Services.Tests;

/// <summary>
///     A cache that always calls through to the factory, so reads reach the database, and records
///     every invalidation so tests can assert on it.
/// </summary>
internal sealed class RecordingCache : ICacheFacade
{
    public List<string> RemovedTags { get; } = [];
    public List<CacheKey> RemovedKeys { get; } = [];

    public static ICacheKeyComposer Keys()
    {
        var keys = Substitute.For<ICacheKeyComposer>();
        keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(TestCacheKeys.FromComposeCall);
        return keys;
    }

    public Task<T?> GetOrAddAsync<T>(CacheKey key, Func<CancellationToken, Task<T?>> factory,
        CacheEntryOptions? options = null, CancellationToken ct = default) => factory(ct);

    public Task SetAsync<T>(CacheKey key, T value, CacheEntryOptions? options = null, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task RemoveAsync(CacheKey key, CancellationToken ct = default)
    {
        RemovedKeys.Add(key);
        return Task.CompletedTask;
    }

    public Task RemoveByTagAsync(string tag, CancellationToken ct = default)
    {
        RemovedTags.Add(tag);
        return Task.CompletedTask;
    }
}
