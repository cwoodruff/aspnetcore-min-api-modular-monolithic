using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Music.Modules.Services;

public class ArtistService(
    IArtistRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys) : IArtistService
{
    private static readonly string[] ArtistTags = ["music:artist", "music:artist:by-id"];

    public async Task<ArtistApiModel?> GetArtistByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "artist",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<ArtistApiModel?>(key, async _ =>
        {
            try
            {
                return await repository.GetById(id);
            }
            catch
            {
                return null;
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = ArtistTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllArtistsAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "artist",
            version: "v1",
            discriminator: "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                var entities = await repository.GetAll();
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = ArtistTags
        }, ct) ?? [];
    }
}
