using FluentValidation;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Services;

public sealed class MediaTypeService(
    IMediaTypeRepository repo,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<MediaTypeApiModel> validator) : IMediaTypeService
{
    private readonly IValidator<MediaTypeApiModel> _validator = validator;
    private static readonly string[] MediaTypeTags = ["administration:mediatype", "administration:mediatype:by-id"];

    public async Task<MediaTypeApiModel?> GetMediaTypeByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "mediatype",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<MediaTypeApiModel?>(key, async _ =>
        {
            try
            {
                var m = await repo.GetById(id);
                return m.Convert();
            }
            catch
            {
                return null;
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = MediaTypeTags
        }, ct);
    }

    public async Task<IEnumerable<MediaTypeApiModel>> GetAllMediaTypesAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "mediatype",
            version: "v1",
            discriminator: "all");

        return await cache.GetOrAddAsync<IEnumerable<MediaTypeApiModel>>(key, async _ =>
        {
            try
            {
                var mediaTypeEntities = await repo.GetAll();
                return mediaTypeEntities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = MediaTypeTags
        }, ct) ?? [];
    }

    public async Task<MediaTypeApiModel?> CreateMediaTypeAsync(MediaTypeApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var created = await repo.Add(entity);

        // Invalidate cache
        await cache.RemoveByTagAsync(MediaTypeTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdateMediaTypeAsync(MediaTypeApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var updated = await repo.Update(entity);

        if (updated)
        {
            // Invalidate cache
            await cache.RemoveByTagAsync(MediaTypeTags[0], ct);
            var key = keys.Compose(
                moduleName: "administration",
                entity: "mediatype",
                version: "v1",
                discriminator: $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
