using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class ArtistRepository(AppDbContext context) : BaseRepository<Artist>(context), IArtistRepository
{
    public async Task<ArtistApiModel?> GetById(int id)
    {
        // Load entity graph with split queries
        var artistEntity = await _context.Artists
            .Where(ar => ar.Id == id)
            .Include(ar => ar.Albums)
            .ThenInclude(al => al.Tracks)
            .AsNoTracking()
            .AsSplitQuery() // important on SQLite for large graphs
            .SingleOrDefaultAsync();

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
