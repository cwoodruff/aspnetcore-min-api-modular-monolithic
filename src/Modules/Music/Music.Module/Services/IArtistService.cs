using SharedKernel.Persistence.ApiModels;

namespace Music.Modules.Services;

public interface IArtistService
{
    Task<ArtistApiModel?> GetArtistByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllArtistsAsync(CancellationToken ct);
}
