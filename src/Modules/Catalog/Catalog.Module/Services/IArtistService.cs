using Catalog.Modules.Models;

namespace Catalog.Modules.Services;

internal interface IArtistService
{
    Task<ArtistApiModel?> GetArtistByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<ArtistApiModel>> GetAllArtistsAsync(CancellationToken ct);
    Task<ArtistApiModel?> CreateArtistAsync(ArtistApiModel model, CancellationToken ct);
    Task<bool> UpdateArtistAsync(ArtistApiModel model, CancellationToken ct);
}
