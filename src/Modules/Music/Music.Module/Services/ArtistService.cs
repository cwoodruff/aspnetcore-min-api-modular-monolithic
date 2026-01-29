using FluentValidation;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Music.Modules.Services;

public class ArtistService(
    IArtistRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<ArtistApiModel> validator) : IArtistService
{
    private static readonly string[] ArtistTags = ["music:artist", "music:artist:by-id"];
    private readonly IValidator<ArtistApiModel> _validator = validator;

    public async Task<ArtistApiModel?> GetArtistByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "artist",
            "v1",
            $"by-id:{id}");

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
            "music",
            "artist",
            "v1",
            "all");

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

    public async Task<ArtistApiModel?> CreateArtistAsync(ArtistApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var created = await repository.Add(entity);

        // Invalidate cache
        await cache.RemoveByTagAsync(ArtistTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdateArtistAsync(ArtistApiModel model, CancellationToken ct)
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
            await cache.RemoveByTagAsync(ArtistTags[0], ct);
            var key = keys.Compose(
                "music",
                "artist",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
