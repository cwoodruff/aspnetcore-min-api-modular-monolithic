using SharedKernel.Persistence.ApiModels;

namespace Catalog.Modules.Services;

internal interface IArtistService
{
    Task<ArtistApiModel?> GetArtistByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<object>> GetAllArtistsAsync(CancellationToken ct);
    Task<ArtistApiModel?> CreateArtistAsync(ArtistApiModel model, CancellationToken ct);
    Task<bool> UpdateArtistAsync(ArtistApiModel model, CancellationToken ct);
}
