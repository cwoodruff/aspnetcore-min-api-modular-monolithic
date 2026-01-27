using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Music.Modules.Services;

public class TrackService(
    ITrackRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys) : ITrackService
{
    private static readonly string[] TrackTags = ["music:track", "music:track:by-id"];

    public async Task<object?> GetTrackByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "track",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<object?>(key, async _ =>
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
            Tags = TrackTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllTracksAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "track",
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
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByArtistIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "track",
            version: "v1",
            discriminator: $"by-artist:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
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
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByPlaylistIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "track",
            version: "v1",
            discriminator: $"by-playlist:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
                var entities = await repository.GetByPlaylistId(id);
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByAlbumIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "track",
            version: "v1",
            discriminator: $"by-album:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
                var entities = await repository.GetByAlbumId(id);
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByGenreIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "track",
            version: "v1",
            discriminator: $"by-genre:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
                var entities = await repository.GetByGenreId(id);
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByMediaTypeIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "track",
            version: "v1",
            discriminator: $"by-mediatype:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
                var entities = await repository.GetByMediaTypeId(id);
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByInvoiceIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "music",
            entity: "track",
            version: "v1",
            discriminator: $"by-invoice:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
                var entities = await repository.GetByInvoiceId(id);
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }
}
