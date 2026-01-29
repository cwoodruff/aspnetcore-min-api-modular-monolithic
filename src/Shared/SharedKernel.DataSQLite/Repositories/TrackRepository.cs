using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class TrackRepository(AppDbContext context) : BaseRepository<Track>(context), ITrackRepository
{
    public async Task<List<Track>> GetByAlbumId(int id)
    {
        return await _context.Tracks.Where(a => a.AlbumId == id)
            .AsNoTracking().ToListAsync();
    }

    public async Task<List<Track>> GetByGenreId(int id)
    {
        return await _context.Tracks.Where(a => a.GenreId == id)
            .AsNoTracking().ToListAsync();
    }

    public async Task<List<Track>> GetByMediaTypeId(int id)
    {
        return await _context.Tracks.Where(a => a.MediaTypeId == id)
            .AsNoTracking().ToListAsync();
    }

    public async Task<List<Track>> GetByPlaylistId(int id)
    {
        return await _context.PlaylistTracks.Where(p => p.PlaylistId == id).Select(p => p.Track!)
            .AsNoTracking().ToListAsync();
    }

    public async Task<List<Track>> GetByArtistId(int id)
    {
        return await _context.Albums.Where(a => a.ArtistId == id).SelectMany(t => t.Tracks!)
            .AsNoTracking().ToListAsync();
    }

    public async Task<List<Track>> GetByInvoiceId(int id)
    {
        return await _context.Tracks.Where(c => c.InvoiceLines!.Any(o => o.InvoiceId == id))
            .AsNoTracking().ToListAsync();
    }

    public async Task<TrackApiModel> GetById(int id)
    {
        return await _context.Tracks
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
                GenreName = t.Genre != null ? t.Genre.Name : null,
                MediaTypeName = t.MediaType != null ? t.MediaType.Name : null,
                Album = null,
                Genre = null,
                MediaType = null,
                Playlists = new List<PlaylistApiModel>(),
                InvoiceLines = new List<InvoiceLineApiModel>()
            })
            .AsNoTracking()
            .SingleAsync();
    }
}
