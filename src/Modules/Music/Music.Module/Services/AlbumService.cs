using FluentValidation;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Music.Modules.Services;

public class AlbumService(
    IAlbumRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<AlbumApiModel> validator) : IAlbumService
{
    private readonly IValidator<AlbumApiModel> _validator = validator;
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
                moduleName: "music",
                entity: "album",
                version: "v1",
                discriminator: $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
