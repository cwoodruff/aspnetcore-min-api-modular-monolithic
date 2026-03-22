using FluentValidation;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Music.Modules.Services;

public class TrackService(
    ITrackRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<TrackApiModel> validator,
    ILogger<TrackService> logger) : ITrackService
{
    private static readonly string[] TrackTags = ["music:track", "music:track:by-id"];
    private readonly ILogger<TrackService> _logger = logger;
    private readonly IValidator<TrackApiModel> _validator = validator;

    public async Task<object?> GetTrackByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "track",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<object?>(key, async _ =>
            await repository.GetById(id)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllTracksAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "track",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetAll();
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByArtistIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "track",
            "v1",
            $"by-artist:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetByArtistId(id);
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByPlaylistIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "track",
            "v1",
            $"by-playlist:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetByPlaylistId(id);
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByAlbumIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "track",
            "v1",
            $"by-album:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetByAlbumId(id);
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByGenreIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "track",
            "v1",
            $"by-genre:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetByGenreId(id);
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByMediaTypeIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "track",
            "v1",
            $"by-mediatype:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetByMediaTypeId(id);
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByInvoiceIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "track",
            "v1",
            $"by-invoice:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetByInvoiceId(id);
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<TrackApiModel?> CreateTrackAsync(TrackApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var created = await repository.Add(entity);

        // Invalidate cache
        await cache.RemoveByTagAsync(TrackTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdateTrackAsync(TrackApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var updated = await repository.Update(entity);

        if (updated)
        {
            // Invalidate cache
            await cache.RemoveByTagAsync(TrackTags[0], ct);
            var key = keys.Compose(
                "music",
                "track",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
