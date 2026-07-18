using FluentValidation;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Music.Modules.Services;

internal class AlbumService(
    IAlbumRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<AlbumApiModel> validator,
    ILogger<AlbumService> logger) : IAlbumService
{
    private static readonly string[] AlbumTags = ["music:album", "music:album:by-id"];
    private readonly ILogger<AlbumService> _logger = logger;
    private readonly IValidator<AlbumApiModel> _validator = validator;

    public async Task<AlbumApiModel?> GetAlbumByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "album",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<AlbumApiModel?>(key, async _ =>
            await repository.GetById(id)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = AlbumTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllAlbumsAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "album",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetAll();
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = AlbumTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetAlbumsByArtistIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "album",
            "v1",
            $"by-artist:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetByArtistId(id);
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = AlbumTags
        }, ct) ?? [];
    }

    public async Task<AlbumApiModel?> CreateAlbumAsync(AlbumApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var created = await repository.Add(entity);

        // Invalidate cache
        await cache.RemoveByTagAsync(AlbumTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdateAlbumAsync(AlbumApiModel model, CancellationToken ct)
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
            await cache.RemoveByTagAsync(AlbumTags[0], ct);
            var key = keys.Compose(
                "music",
                "album",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
