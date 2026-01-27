using FluentValidation;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Services;

public sealed class GenreService(
    IGenreRepository repo,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<GenreApiModel> validator) : IGenreService
{
    private static readonly string[] GenreTags = ["administration:genre", "administration:genre:by-id"];

    public async Task<GenreApiModel?> GetGenreByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "genre",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<GenreApiModel?>(key, async _ =>
        {
            try
            {
                var g = await repo.GetById(id);
                return g.Convert();
            }
            catch
            {
                return null;
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = GenreTags
        }, ct);
    }

    public async Task<IEnumerable<GenreApiModel>> GetAllGenresAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "genre",
            version: "v1",
            discriminator: "all");

        return await cache.GetOrAddAsync<IEnumerable<GenreApiModel>>(key, async _ =>
        {
            try
            {
                var genreEntities = await repo.GetAll();
                return genreEntities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = GenreTags
        }, ct) ?? [];
    }

    public async Task<GenreApiModel?> CreateGenreAsync(string name, CancellationToken ct)
    {
        var model = new GenreApiModel { Name = name };
        var result = await validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var genre = new Genre { Name = name };
        var created = await repo.Add(genre);

        // Invalidate cache
        await cache.RemoveByTagAsync(GenreTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdateGenreAsync(int id, string name, CancellationToken ct)
    {
        var model = new GenreApiModel { Id = id, Name = name };
        var result = await validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var genre = new Genre { Id = id, Name = name };
        var updated = await repo.Update(genre);

        if (updated)
        {
            // Invalidate cache
            await cache.RemoveByTagAsync(GenreTags[0], ct);
            var key = keys.Compose(
                moduleName: "administration",
                entity: "genre",
                version: "v1",
                discriminator: $"by-id:{id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }

    public async Task<bool> DeleteGenreAsync(int id, CancellationToken ct)
    {
        var deleted = await repo.Delete(id);

        if (deleted)
        {
            // Invalidate cache
            await cache.RemoveByTagAsync(GenreTags[0], ct);
            var key = keys.Compose(
                moduleName: "administration",
                entity: "genre",
                version: "v1",
                discriminator: $"by-id:{id}");
            await cache.RemoveAsync(key, ct);
        }

        return deleted;
    }
}
