using Catalog.Modules.Data;
using Catalog.Modules.Domain;
using Catalog.Modules.Mapping;
using Catalog.Modules.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;

namespace Catalog.Modules.Services;

internal class ArtistService(
    CatalogDbContext db,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<ArtistApiModel> validator,
    ILogger<ArtistService> logger) : IArtistService
{
    private static readonly string[] ArtistTags = ["catalog:artist", "catalog:artist:by-id"];
    private readonly ILogger<ArtistService> _logger = logger;
    private readonly IValidator<ArtistApiModel> _validator = validator;

    public async Task<ArtistApiModel?> GetArtistByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "artist",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<ArtistApiModel?>(key, async _ =>
            await LoadByIdAsync(id, ct)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = ArtistTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllArtistsAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "artist",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await db.Artists.AsNoTracking().ToListAsync(ct);
            return entities.ToApiModels();
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

        var entity = model.ToEntity();
        db.Artists.Add(entity);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync(ArtistTags[0], ct);

        return entity.ToApiModel();
    }

    public async Task<bool> UpdateArtistAsync(ArtistApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        var updated = await db.Artists.AnyAsync(e => e.Id == entity.Id, ct);
        if (updated)
        {
            db.Artists.Update(entity);
            await db.SaveChangesAsync(ct);

            // Invalidate cache
            await cache.RemoveByTagAsync(ArtistTags[0], ct);
            var key = keys.Compose(
                "catalog",
                "artist",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }

    private async Task<ArtistApiModel?> LoadByIdAsync(int id, CancellationToken ct)
    {
        // Load entity graph with split queries
        var artistEntity = await db.Artists
            .Where(ar => ar.Id == id)
            .Include(ar => ar.Albums)
            .ThenInclude(al => al.Tracks)
            .AsNoTracking()
            .AsSplitQuery() // important on SQLite for large graphs
            .SingleOrDefaultAsync(ct);

        if (artistEntity is null)
            return null;

        // Project to DTO in memory (no APPLY needed)
        var artistDto = new ArtistApiModel
        {
            Id = artistEntity.Id,
            Name = artistEntity.Name,
            Albums = artistEntity.Albums.Select(al => new AlbumApiModel
            {
                Id = al.Id,
                Title = al.Title,
                ArtistId = al.ArtistId,
                ArtistName = artistEntity.Name,
                Artist = null,
                Tracks = al.Tracks.Select(t => new TrackApiModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    AlbumId = t.AlbumId,
                    GenreId = t.GenreId,
                    MediaTypeId = t.MediaTypeId,
                    Composer = t.Composer,
                    Milliseconds = t.Milliseconds,
                    Bytes = t.Bytes,
                    UnitPrice = t.UnitPrice,
                    AlbumName = al.Title,
                    Album = null,
                    Playlists = new List<PlaylistApiModel>()
                }).ToList()
            }).ToList()
        };

        return artistDto;
        }
}
