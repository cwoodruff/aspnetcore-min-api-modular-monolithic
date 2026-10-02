using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using SharedKernel.Caching;

namespace ModularMonolith.Module.Tests.Kernel;

/// <summary>Each module's cache is its own MemoryCache with its own size limit (ADR-0013).</summary>
public sealed class ModuleCacheTests : IDisposable
{
    // MemoryCache compacts a full cache down to SizeLimit x (1 - CompactionPercentage), default 5%, rounding
    // the excess down to whole entries. Below 20 that excess rounds to zero and a full cache only ever refuses
    // new entries; the modules' limits (500 and 1000) evict 25 to 50 entries per compaction.
    private const int Limit = 100;
    private readonly ServiceProvider _services;

    public ModuleCacheTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddReflectionJsonSerialization();
        services.AddModuleCache("Catalog", Limit);
        services.AddModuleCache("Administration", Limit);
        _services = services.BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task GoingPastOneModulesLimit_EvictsInThatModule_AndLeavesTheOtherModuleAlone()
    {
        var catalog = _services.GetRequiredKeyedService<ICacheFacade>("Catalog");
        var administration = _services.GetRequiredKeyedService<ICacheFacade>("Administration");
        var catalogMemory = (MemoryCache)_services.GetRequiredKeyedService<IMemoryCache>("Catalog");
        var administrationMemory = (MemoryCache)_services.GetRequiredKeyedService<IMemoryCache>("Administration");

        for (var i = 0; i < 5; i++)
        {
            await administration.SetAsync(Key("administration", i), $"admin-{i}");
        }

        for (var i = 0; i < Limit; i++)
        {
            await catalog.SetAsync(Key("catalog", i), $"catalog-{i}");
        }

        catalogMemory.Count.Should().Be(Limit);

        // One past the limit: MemoryCache refuses the entry and compacts in the background, evicting
        // existing Catalog entries to get back under the limit.
        await catalog.SetAsync(Key("catalog", Limit), "over the limit");
        catalogMemory.Count.Should().BeLessThanOrEqualTo(Limit, "the module's cache never holds more than its limit");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (catalogMemory.Count >= Limit && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        catalogMemory.Count.Should().BeLessThan(Limit, "compaction evicted at least one existing Catalog entry");
        var evicted = 0;
        for (var i = 0; i < Limit; i++)
        {
            if (!catalogMemory.TryGetValue(Key("catalog", i).ToString(), out _))
            {
                evicted++;
            }
        }

        evicted.Should().BePositive();

        // With room again, a new entry is cached.
        await catalog.SetAsync(Key("catalog", Limit + 1), "after eviction");
        (await catalog.GetOrAddAsync<string>(Key("catalog", Limit + 1), _ => Task.FromResult<string?>("reloaded")))
            .Should().Be("after eviction");

        // Administration lost nothing: every entry is still served from its own cache.
        administrationMemory.Count.Should().Be(5);
        for (var i = 0; i < 5; i++)
        {
            var loaded = false;
            var value = await administration.GetOrAddAsync<string>(Key("administration", i), _ =>
            {
                loaded = true;
                return Task.FromResult<string?>("reloaded");
            });
            value.Should().Be($"admin-{i}");
            loaded.Should().BeFalse();
        }
    }

    private static CacheKey Key(string module, int i) =>
        new("test", "app", module, "item", "v1", null, null, null, $"by-id:{i}");
}
