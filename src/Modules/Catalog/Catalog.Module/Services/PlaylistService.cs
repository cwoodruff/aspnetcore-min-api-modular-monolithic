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

internal class PlaylistService(
    CatalogDbContext db,
    [FromKeyedServices(CatalogModule.ModuleName)] ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<PlaylistApiModel> validator,
    ILogger<PlaylistService> logger) : IPlaylistService
{
    private static readonly string[] PlaylistTags = ["catalog:playlist", "catalog:playlist:by-id"];
    private readonly ILogger<PlaylistService> _logger = logger;
    private readonly IValidator<PlaylistApiModel> _validator = validator;

    public async Task<PlaylistApiModel?> GetPlaylistByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "playlist",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<PlaylistApiModel?>(key, async _ =>
            await LoadByIdAsync(id, ct)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = PlaylistTags
        }, ct);
    }

    public async Task<IReadOnlyList<PlaylistApiModel>> GetAllPlaylistsAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "catalog",
            "playlist",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IReadOnlyList<PlaylistApiModel>>(key, async _ =>
        {
            var entities = await db.Playlists.AsNoTracking().ToListAsync(ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = PlaylistTags
        }, ct) ?? [];
    }

    public async Task<PlaylistApiModel?> CreatePlaylistAsync(PlaylistApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        db.Playlists.Add(entity);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync(PlaylistTags[0], ct);

        return entity.ToApiModel();
    }

    public async Task<bool> UpdatePlaylistAsync(PlaylistApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        var updated = await db.Playlists.AnyAsync(e => e.Id == entity.Id, ct);
        if (updated)
        {
            db.Playlists.Update(entity);
            await db.SaveChangesAsync(ct);

            // Invalidate cache
            await cache.RemoveByTagAsync(PlaylistTags[0], ct);
            var key = keys.Compose(
                "catalog",
                "playlist",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }

    private async Task<PlaylistApiModel?> LoadByIdAsync(int id, CancellationToken ct)
    {
        // Option A: Two lean queries with direct projection to DTOs (no entity graph materialization)
        var header = await db.Playlists
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.Id, p.Name })
            .SingleOrDefaultAsync(ct);

        if (header is null)
            return null;

        var tracks = await db.PlaylistTracks
            .AsNoTracking()
            .Where(pt => pt.PlaylistId == id)
            .Select(pt => new TrackApiModel
            {
                Id = pt.Track.Id,
                Name = pt.Track.Name,
                AlbumId = pt.Track.AlbumId,
                GenreId = pt.Track.GenreId,
                MediaTypeId = pt.Track.MediaTypeId,
                Composer = pt.Track.Composer,
                Milliseconds = pt.Track.Milliseconds,
                Bytes = pt.Track.Bytes,
                UnitPrice = pt.Track.UnitPrice,
                AlbumName = pt.Track.Album != null ? pt.Track.Album.Title : null,
                Album = null,
                Playlists = new List<PlaylistApiModel>()
            })
            .OrderBy(t => t.Id)
            .ToListAsync(ct);

        return new PlaylistApiModel
        {
            Id = header.Id,
            Name = header.Name,
            Tracks = tracks
        };
    }
}
