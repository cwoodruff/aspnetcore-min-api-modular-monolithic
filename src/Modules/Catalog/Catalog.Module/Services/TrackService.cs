using Catalog.Modules.Data;
using Catalog.Modules.Domain;
using Catalog.Modules.Mapping;
using Catalog.Modules.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;

namespace Catalog.Modules.Services;

internal class TrackService(
    CatalogDbContext db,
    [FromKeyedServices(CatalogModule.ModuleName)] ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<TrackApiModel> validator,
    ILogger<TrackService> logger) : ITrackService
{
    private static readonly string[] TrackTags = ["catalog:track", "catalog:track:by-id"];
    private readonly ILogger<TrackService> _logger = logger;
    private readonly IValidator<TrackApiModel> _validator = validator;

    public async Task<object?> GetTrackByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "track",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<object?>(key, async _ =>
            await LoadByIdAsync(id, ct)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllTracksAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "track",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await db.Tracks.AsNoTracking().ToListAsync(ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByArtistIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "track",
            "v1",
            $"by-artist:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await LoadByArtistIdAsync(id, ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByPlaylistIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "track",
            "v1",
            $"by-playlist:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await LoadByPlaylistIdAsync(id, ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByAlbumIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "track",
            "v1",
            $"by-album:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await LoadByAlbumIdAsync(id, ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByGenreIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "track",
            "v1",
            $"by-genre:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await LoadByGenreIdAsync(id, ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetTracksByMediaTypeIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "track",
            "v1",
            $"by-mediatype:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await LoadByMediaTypeIdAsync(id, ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = TrackTags
        }, ct) ?? [];
    }

    public async Task<TrackApiModel?> CreateTrackAsync(TrackApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        db.Tracks.Add(entity);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync(TrackTags[0], ct);

        return entity.ToApiModel();
    }

    public async Task<bool> UpdateTrackAsync(TrackApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        var updated = await db.Tracks.AnyAsync(e => e.Id == entity.Id, ct);
        if (updated)
        {
            db.Tracks.Update(entity);
            await db.SaveChangesAsync(ct);

            // Invalidate cache
            await cache.RemoveByTagAsync(TrackTags[0], ct);
            var key = keys.Compose(
                "catalog",
                "track",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }

    /// <returns>Null if the track does not exist; zero sales if none have been counted yet.</returns>
    /// <remarks>Not cached: the counts change whenever the orders outbox delivers.</remarks>
    public async Task<TrackSalesApiModel?> GetTrackSalesAsync(int id, CancellationToken ct)
    {
        if (!await db.Tracks.AnyAsync(t => t.Id == id, ct))
        {
            return null;
        }

        var sales = await db.TrackSales.AsNoTracking().SingleOrDefaultAsync(s => s.TrackId == id, ct);
        return new TrackSalesApiModel(id, sales?.TimesSold ?? 0, sales?.LastSoldAt);
    }

    private async Task<List<Track>> LoadByAlbumIdAsync(int id, CancellationToken ct)
    {
        return await db.Tracks.Where(a => a.AlbumId == id)
            .AsNoTracking().ToListAsync(ct);
    }

    private async Task<List<Track>> LoadByGenreIdAsync(int id, CancellationToken ct)
    {
        return await db.Tracks.Where(a => a.GenreId == id)
            .AsNoTracking().ToListAsync(ct);
    }

    private async Task<List<Track>> LoadByMediaTypeIdAsync(int id, CancellationToken ct)
    {
        return await db.Tracks.Where(a => a.MediaTypeId == id)
            .AsNoTracking().ToListAsync(ct);
    }

    private async Task<List<Track>> LoadByPlaylistIdAsync(int id, CancellationToken ct)
    {
        return await db.PlaylistTracks.Where(p => p.PlaylistId == id).Select(p => p.Track!)
            .AsNoTracking().ToListAsync(ct);
    }

    private async Task<List<Track>> LoadByArtistIdAsync(int id, CancellationToken ct)
    {
        return await db.Albums.Where(a => a.ArtistId == id).SelectMany(t => t.Tracks!)
            .AsNoTracking().ToListAsync(ct);
    }

    private async Task<TrackApiModel?> LoadByIdAsync(int id, CancellationToken ct)
    {
        return await db.Tracks
            .Where(t => t.Id == id)
            .Select(t => new TrackApiModel
            {
                Id = t.Id,
                Name = t.Name,
                AlbumId = t.AlbumId,
                MediaTypeId = t.MediaTypeId,
                GenreId = t.GenreId,
                Composer = t.Composer,
                Milliseconds = t.Milliseconds,
                Bytes = t.Bytes,
                UnitPrice = t.UnitPrice,
                AlbumName = t.Album != null ? t.Album.Title : null,
                Album = null,
                Playlists = new List<PlaylistApiModel>()
            })
            .AsNoTracking()
            .SingleOrDefaultAsync(ct);
    }
}
