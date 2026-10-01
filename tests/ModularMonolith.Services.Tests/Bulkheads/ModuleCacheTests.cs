using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Caching;

namespace ModularMonolith.Services.Tests.Bulkheads;

/// <summary>Each module's cache is its own MemoryCache with its own size limit (ADR-0013).</summary>
public sealed class ModuleCacheTests : IDisposable
{
    private const int Limit = 10;
    private readonly ServiceProvider _services;

    public ModuleCacheTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddModuleCache("Catalog", Limit);
        services.AddModuleCache("Administration", Limit);
        _services = services.BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task FillingOneModulesCachePastItsLimit_StaysWithinTheLimit_AndLeavesTheOtherModuleAlone()
    {
        var catalog = _services.GetRequiredKeyedService<ICacheFacade>("Catalog");
        var administration = _services.GetRequiredKeyedService<ICacheFacade>("Administration");

        for (var i = 0; i < 5; i++)
        {
            await administration.SetAsync(Key("administration", i), $"admin-{i}");
        }

        for (var i = 0; i < Limit * 5; i++)
        {
            await catalog.SetAsync(Key("catalog", i), $"catalog-{i}");
        }

        var catalogMemory = (MemoryCache)_services.GetRequiredKeyedService<IMemoryCache>("Catalog");
        catalogMemory.Count.Should().BeLessThanOrEqualTo(Limit, "the module's cache never holds more than its limit");

        // Every Administration entry is still served from its cache: the factory never runs.
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

        ((MemoryCache)_services.GetRequiredKeyedService<IMemoryCache>("Administration")).Count.Should().Be(5);
    }

    private static CacheKey Key(string module, int i) =>
        new("test", "app", module, "item", "v1", null, null, null, $"by-id:{i}");
}
