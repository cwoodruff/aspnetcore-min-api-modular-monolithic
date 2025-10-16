using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
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

        // Optionally register L2 provider (keep provider-agnostic; default to in-memory distributed)
        var provider = configuration["Caching:Provider"] ?? "InMemory";
        var tier = configuration["Caching:Tier"] ?? "L1";

        if (string.Equals(tier, "L1L2", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                services.AddDistributedMemoryCache();
            }
            // For Redis/others, assume host adds the specific provider package and registration.
            // We still resolve IDistributedCache if available.

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
