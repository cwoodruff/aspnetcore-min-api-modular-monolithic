using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Music.Modules.Services;

public class AlbumService(
    IAlbumRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys) : IAlbumService
{
    private static readonly string[] AlbumTags = ["music:album", "music:album:by-id"];

    public async Task<AlbumApiModel?> GetAlbumByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "album",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<AlbumApiModel?>(key, async _ =>
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
            Tags = AlbumTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllAlbumsAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "album",
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
            Tags = AlbumTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetAlbumsByArtistIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "album",
            version: "v1",
            discriminator: $"by-artist:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                var entities = await repository.GetByArtistId(id);
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = AlbumTags
        }, ct) ?? [];
    }
}
