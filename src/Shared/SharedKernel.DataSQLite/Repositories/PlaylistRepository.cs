using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class PlaylistRepository(AppDbContext context) : BaseRepository<Playlist>(context), IPlaylistRepository
{
    public async Task<List<Playlist>> GetByTrackId(int id)
    {
        return await _context.PlaylistTracks.Where(p => p.TrackId == id).Select(p => p.Playlist!)
            .AsNoTracking().ToListAsync();
    }

    public async Task<PlaylistApiModel> GetById(int id)
    {
        // Option A: Two lean queries with direct projection to DTOs (no entity graph materialization)
        var header = await _context.Playlists
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.Id, p.Name })
            .SingleAsync();

        var tracks = await _context.PlaylistTracks
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
                GenreName = pt.Track.Genre != null ? pt.Track.Genre.Name : null,
                MediaTypeName = pt.Track.MediaType != null ? pt.Track.MediaType.Name : null,
                Album = null,
                Genre = null,
                MediaType = null,
                Playlists = new List<PlaylistApiModel>(),
                InvoiceLines = new List<InvoiceLineApiModel>()
            })
            .OrderBy(t => t.Id)
            .ToListAsync();

        return new PlaylistApiModel
        {
            Id = header.Id,
            Name = header.Name,
            Tracks = tracks
        };
    }
}
