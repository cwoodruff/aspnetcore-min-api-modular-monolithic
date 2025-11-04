using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class PlaylistRepository(AppDbContext context) : BaseRepository<Playlist>(context), IPlaylistRepository
{
    public async Task<List<Playlist>> GetByTrackId(int id) =>
        await _context.PlaylistTracks.Where(p => p.TrackId == id).Select(p => p.Playlist!)
            .AsNoTracking().ToListAsync();

    public async Task<PlaylistApiModel> GetById(int id)
    {
        // Entities load (split queries), then manual projection to DTO
        var playlistEntity = await _context.Playlists
            .Where(p => p.Id == id)
            .Include(p => p.Tracks)
                .ThenInclude(t => t.Album)
            .Include(p => p.Tracks)
                .ThenInclude(t => t.Genre)
            .Include(p => p.Tracks)
                .ThenInclude(t => t.MediaType)
            .AsNoTracking()
            .AsSplitQuery()
            .SingleAsync();

        var playlistDto = new PlaylistApiModel
        {
            Id = playlistEntity.Id,
            Name = playlistEntity.Name,
            Tracks = playlistEntity.Tracks.Select(t => new TrackApiModel
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
                AlbumName = t.Album?.Title,
                GenreName = t.Genre?.Name,
                MediaTypeName = t.MediaType?.Name,
                Album = null,
                Genre = null,
                MediaType = null,
                Playlists = new List<PlaylistApiModel>(),
                InvoiceLines = new List<InvoiceLineApiModel>()
            }).ToList()
        };
        return playlistDto;
    }
}
