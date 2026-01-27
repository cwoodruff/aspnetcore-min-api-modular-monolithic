using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Music.Modules.Services;

public class PlaylistService(
    IPlaylistRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys) : IPlaylistService
{
    private static readonly string[] PlaylistTags = ["music:playlist", "music:playlist:by-id"];

    public async Task<PlaylistApiModel?> GetPlaylistByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "playlist",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<PlaylistApiModel?>(key, async _ =>
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
            Tags = PlaylistTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllPlaylistsAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "playlist",
            version: "v1",
            discriminator: "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
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
            Tags = PlaylistTags
        }, ct) ?? [];
    }
}
