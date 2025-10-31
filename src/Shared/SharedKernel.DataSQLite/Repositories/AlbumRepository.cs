using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class AlbumRepository(AppDbContext context) : BaseRepository<Album>(context), IAlbumRepository
{
    public async Task<List<Album>> GetByArtistId(int id) =>
        await _context.Albums
            .Where(a => a.ArtistId == id)
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .AsNoTracking()
            .ToListAsync();

    public async Task<AlbumApiModel> GetById(int id)
    {
        // Album with Tracks (and Artist) via split queries, no tracking
        var albumEntity = await _context.Albums
            .Where(a => a.Id == id)
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .ThenInclude(t => t.Genre)       // optional if you need GenreName
            .Include(a => a.Tracks)
            .ThenInclude(t => t.MediaType)   // optional if you need MediaTypeName
            .AsNoTracking()
            .AsSplitQuery()   // important on SQLite to avoid cartesian explosion
            .SingleAsync();

        var albumDto = new AlbumApiModel
        {
            Id = albumEntity.Id,
            Title = albumEntity.Title,
            ArtistId = albumEntity.ArtistId,
            ArtistName = albumEntity.Artist?.Name,
            Artist = albumEntity.Artist == null ? null : new ArtistApiModel
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
                GenreName = t.Genre?.Name,
                MediaTypeName = t.MediaType?.Name,
                // keep nested objects null to avoid cycles
                Album = null,
                Genre = null,
                MediaType = null,
                Playlists = new List<PlaylistApiModel>(),
                InvoiceLines = new List<InvoiceLineApiModel>()
            }).ToList()
        };
        return albumDto;
    }
}
