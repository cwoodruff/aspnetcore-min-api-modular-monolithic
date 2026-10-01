using Admin.Modules.Data;
using Admin.Modules.Domain;
using Admin.Modules.Mapping;
using Admin.Modules.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;

namespace Admin.Modules.Services;

internal sealed class GenreService(
    AdministrationDbContext db,
    [FromKeyedServices(AdministrationModule.ModuleName)] ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<GenreApiModel> validator,
    ILogger<GenreService> logger) : IGenreService
{
    private static readonly string[] GenreTags = ["administration:genre", "administration:genre:by-id"];
    private readonly ILogger<GenreService> _logger = logger;

    public async Task<GenreApiModel?> GetGenreByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "genre",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<GenreApiModel?>(key, async _ =>
        {
            var g = await LoadByIdAsync(id, ct);
            return g?.ToApiModel();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = GenreTags
        }, ct);
    }

    public async Task<IEnumerable<GenreApiModel>> GetAllGenresAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "genre",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<GenreApiModel>>(key, async _ =>
        {
            var genreEntities = await db.Genres.AsNoTracking().ToListAsync(ct);
            return genreEntities.ToApiModels();
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
        db.Genres.Add(genre);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync(GenreTags[0], ct);

        return genre.ToApiModel();
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
        var updated = await db.Genres.AnyAsync(e => e.Id == genre.Id, ct);
        if (updated)
        {
            db.Genres.Update(genre);
            await db.SaveChangesAsync(ct);

            // Invalidate cache
            await cache.RemoveByTagAsync(GenreTags[0], ct);
            var key = keys.Compose(
                "administration",
                "genre",
                "v1",
                $"by-id:{id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }

    public async Task<bool> DeleteGenreAsync(int id, CancellationToken ct)
    {
        var toDelete = await db.Genres.FindAsync([id], ct);
        if (toDelete is not null)
        {
            db.Genres.Remove(toDelete);
            await db.SaveChangesAsync(ct);
        }

        var deleted = toDelete is not null;

        if (deleted)
        {
            // Invalidate cache
            await cache.RemoveByTagAsync(GenreTags[0], ct);
            var key = keys.Compose(
                "administration",
                "genre",
                "v1",
                $"by-id:{id}");
            await cache.RemoveAsync(key, ct);
        }

        return deleted;
    }

    private async Task<Genre?> LoadByIdAsync(int id, CancellationToken ct)
    {
        return await db.Genres
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id, ct);
    }
}
