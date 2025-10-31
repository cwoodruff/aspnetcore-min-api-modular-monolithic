using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;

namespace SharedKernel.Persistence.Repositories;

public interface ITrackRepository : IRepository<Track>, IDisposable
{
    Task<List<Track>> GetByAlbumId(int id);
    Task<List<Track>> GetByGenreId(int id);
    Task<List<Track>> GetByMediaTypeId(int id);
    Task<List<Track>> GetByInvoiceId(int id);
    Task<List<Track>> GetByPlaylistId(int id);
    Task<List<Track>> GetByArtistId(int id);
    Task<TrackApiModel> GetById(int id);
}
