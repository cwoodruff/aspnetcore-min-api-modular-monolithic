using Microsoft.Extensions.Configuration;

namespace SharedKernel.Caching;

public interface ICacheKeyComposer
{
    CacheKey Compose(
        string moduleName,
        string entity,
        string version,
        string discriminator,
        string? tenant = null,
        string? locale = null,
        string? feature = null);
}

internal sealed class CacheKeyComposer(IConfiguration config) : ICacheKeyComposer
{
    private readonly string _app = (config["ServiceName"] ?? "mmapi").ToLowerInvariant();

    private readonly string _env =
        (config["ASPNETCORE_ENVIRONMENT"] ?? config["DOTNET_ENVIRONMENT"] ?? "prod").ToLowerInvariant();

    public CacheKey Compose(string moduleName, string entity, string version, string discriminator,
        string? tenant = null, string? locale = null, string? feature = null)
    {
        return new CacheKey(
            _env,
            _app,
            moduleName.ToLowerInvariant(),
            entity.ToLowerInvariant(),
            version.ToLowerInvariant(),
            tenant?.ToLowerInvariant(),
            locale?.ToLowerInvariant(),
            feature?.ToLowerInvariant(),
            discriminator
        );
    }
}
