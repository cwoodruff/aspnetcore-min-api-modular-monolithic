using Catalog.Modules.Domain;
using Catalog.Modules.Models;
using Catalog.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Modules.Endpoints;

/// <summary>The artist endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class ArtistHandlers
{
    [Authorize]
    public static async Task<Results<Ok<ArtistApiModel>, NotFound>> GetArtistById(int id, IArtistService service, CancellationToken ct)
    {
        var artist = await service.GetArtistByIdAsync(id, ct);
        return artist is not null ? TypedResults.Ok(artist) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<ArtistApiModel>>> GetAllArtists(IArtistService service, CancellationToken ct)
    {
        var artists = await service.GetAllArtistsAsync(ct);
        return TypedResults.Ok(artists);
    }
}
