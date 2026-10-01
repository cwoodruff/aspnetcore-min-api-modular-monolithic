using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class AlbumRepository(AppDbContext context) : BaseRepository<Album>(context), IAlbumRepository
{
    public async Task<List<Album>> GetByArtistId(int id)
    {
        return await _context.Albums
            .Where(a => a.ArtistId == id)
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<AlbumApiModel?> GetById(int id)
    {
        // Album with Tracks (and Artist) via split queries, no tracking
        var albumEntity = await _context.Albums
            .Where(a => a.Id == id)
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .AsNoTracking()
            .AsSplitQuery() // important on SQLite to avoid cartesian explosion
            .SingleOrDefaultAsync();

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
