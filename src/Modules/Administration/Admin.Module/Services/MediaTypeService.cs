using Admin.Modules.Data;
using Admin.Modules.Domain;
using Admin.Modules.Mapping;
using Admin.Modules.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;

namespace Admin.Modules.Services;

internal sealed class MediaTypeService(
    AdministrationDbContext db,
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
            var m = await LoadByIdAsync(id, ct);
            return m?.ToApiModel();
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
            var mediaTypeEntities = await db.MediaTypes.AsNoTracking().ToListAsync(ct);
            return mediaTypeEntities.ToApiModels();
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

        var entity = model.ToEntity();
        db.MediaTypes.Add(entity);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync(MediaTypeTags[0], ct);

        return entity.ToApiModel();
    }

    public async Task<bool> UpdateMediaTypeAsync(MediaTypeApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        var updated = await db.MediaTypes.AnyAsync(e => e.Id == entity.Id, ct);
        if (updated)
        {
            db.MediaTypes.Update(entity);
            await db.SaveChangesAsync(ct);

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

    private async Task<MediaType?> LoadByIdAsync(int id, CancellationToken ct)
    {
        return await db.MediaTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id, ct);
    }
}
