using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Diagnostics;

namespace SharedKernel.Caching;

public static class CachingRegistration
{
    /// <summary>
    /// Gives a module its own memory cache, capped at <paramref name="sizeLimit" /> entries, and its own
    /// <see cref="ICacheFacade" />, resolved keyed by <paramref name="moduleName" /> (ADR-0013). Filling one
    /// module's cache cannot evict another's. The key composer and the optional shared L2 (Caching:Tier =
    /// L1L2) are registered once.
    /// </summary>
    public static IServiceCollection AddModuleCache(this IServiceCollection services, string moduleName, long sizeLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeLimit);

        services.AddOptions<CacheOptions>().BindConfiguration("Caching");
        services.TryAddSingleton<ICacheKeyComposer, CacheKeyComposer>();
        services.TryAddSingleton<SharedL2Cache>();
        services.AddModuleMeter(moduleName);

        services.AddKeyedSingleton<IMemoryCache>(moduleName,
            (_, _) => new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit }));
        services.AddKeyedSingleton<ICacheFacade>(moduleName, (sp, _) => new CompositeCacheFacade(
            sp.GetRequiredService<IOptions<CacheOptions>>(),
            new L1MemoryCacheAdapter(sp.GetRequiredKeyedService<IMemoryCache>(moduleName)),
            sp.GetRequiredKeyedService<ModuleMeter>(moduleName),
            sp.GetRequiredService<ILogger<CompositeCacheFacade>>(),
            sp.GetRequiredService<SharedL2Cache>().Cache));
        return services;
    }

    /// <summary>The one L2 every module shares when Caching:Tier is L1L2; null otherwise.</summary>
    internal sealed class SharedL2Cache(IOptions<CacheOptions> options, IServiceProvider services)
    {
        public IL2Cache? Cache { get; } = Create(options.Value, services);

        private static L2DistributedCacheAdapter? Create(CacheOptions options, IServiceProvider services)
        {
            if (!options.Tier.Equals("L1L2", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // InMemory falls back to a process-local distributed cache; other providers are registered by the host.
            var distributed = services.GetService<IDistributedCache>()
                              ?? (options.Provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
                                  ? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))
                                  : null);
            return distributed is null ? null : new L2DistributedCacheAdapter(distributed);
        }
    }
}
