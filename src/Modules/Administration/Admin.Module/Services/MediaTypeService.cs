using FluentValidation;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Services;

internal sealed class MediaTypeService(
    IMediaTypeRepository repo,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<MediaTypeApiModel> validator,
    ILogger<MediaTypeService> logger) : IMediaTypeService
{
    private static readonly string[] MediaTypeTags = ["administration:mediatype", "administration:mediatype:by-id"];
    private readonly ILogger<MediaTypeService> _logger = logger;
    private readonly IValidator<MediaTypeApiModel> _validator = validator;

    public async Task<MediaTypeApiModel?> GetMediaTypeByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "mediatype",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<MediaTypeApiModel?>(key, async _ =>
        {
            var m = await repo.GetById(id);
            return m?.Convert();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = MediaTypeTags
        }, ct);
    }

    public async Task<IEnumerable<MediaTypeApiModel>> GetAllMediaTypesAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "mediatype",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<MediaTypeApiModel>>(key, async _ =>
        {
            var mediaTypeEntities = await repo.GetAll();
            return mediaTypeEntities.ConvertAll();
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
                "administration",
                "mediatype",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
