using Catalog.Modules.Data;
using Catalog.Modules.Domain;
using Catalog.Modules.Mapping;
using Catalog.Modules.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;

namespace Catalog.Modules.Services;

internal class AlbumService(
    CatalogDbContext db,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<AlbumApiModel> validator,
    ILogger<AlbumService> logger) : IAlbumService
{
    private static readonly string[] AlbumTags = ["catalog:album", "catalog:album:by-id"];
    private readonly ILogger<AlbumService> _logger = logger;
    private readonly IValidator<AlbumApiModel> _validator = validator;

    public async Task<AlbumApiModel?> GetAlbumByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "album",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<AlbumApiModel?>(key, async _ =>
            await LoadByIdAsync(id, ct)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = AlbumTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllAlbumsAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "album",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await db.Albums.AsNoTracking().ToListAsync(ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = AlbumTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetAlbumsByArtistIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "album",
            "v1",
            $"by-artist:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await LoadByArtistIdAsync(id, ct);
            return entities.ToApiModels();
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

        var entity = model.ToEntity();
        db.Albums.Add(entity);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync(AlbumTags[0], ct);

        return entity.ToApiModel();
    }

    public async Task<bool> UpdateAlbumAsync(AlbumApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        var updated = await db.Albums.AnyAsync(e => e.Id == entity.Id, ct);
        if (updated)
        {
            db.Albums.Update(entity);
            await db.SaveChangesAsync(ct);

            // Invalidate cache
            await cache.RemoveByTagAsync(AlbumTags[0], ct);
            var key = keys.Compose(
                "catalog",
                "album",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }

    private async Task<List<Album>> LoadByArtistIdAsync(int id, CancellationToken ct)
    {
        return await db.Albums
            .Where(a => a.ArtistId == id)
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .AsNoTracking()
            .ToListAsync(ct);
        }

    private async Task<AlbumApiModel?> LoadByIdAsync(int id, CancellationToken ct)
    {
        // Album with Tracks (and Artist) via split queries, no tracking
        var albumEntity = await db.Albums
            .Where(a => a.Id == id)
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .AsNoTracking()
            .AsSplitQuery() // important on SQLite to avoid cartesian explosion
            .SingleOrDefaultAsync(ct);

        if (albumEntity is null)
            return null;

        var albumDto = new AlbumApiModel
        {
            Id = albumEntity.Id,
            Title = albumEntity.Title,
            ArtistId = albumEntity.ArtistId,
            ArtistName = albumEntity.Artist?.Name,
            Artist = albumEntity.Artist == null
                ? null
                : new ArtistApiModel
                {
                    Id = albumEntity.Artist.Id,
                    Name = albumEntity.Artist.Name
                },
            Tracks = albumEntity.Tracks.Select(t => new TrackApiModel
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
                AlbumName = albumEntity.Title,
                // keep nested objects null to avoid cycles
                Album = null,
                Playlists = new List<PlaylistApiModel>()
            }).ToList()
        };
        return albumDto;
        }
}
