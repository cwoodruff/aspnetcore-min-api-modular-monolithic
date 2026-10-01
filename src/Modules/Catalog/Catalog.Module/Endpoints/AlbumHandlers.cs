using Catalog.Modules.Domain;
using Catalog.Modules.Models;
using Catalog.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Modules.Endpoints;

/// <summary>The album endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class AlbumHandlers
{
    [Authorize]
    public static async Task<Results<Ok<AlbumApiModel>, NotFound>> GetAlbumById(int id, IAlbumService service, CancellationToken ct)
    {
        var album = await service.GetAlbumByIdAsync(id, ct);
        return album is not null ? TypedResults.Ok(album) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<AlbumApiModel>>> GetAllAlbums(IAlbumService service, CancellationToken ct)
    {
        var albums = await service.GetAllAlbumsAsync(ct);
        return TypedResults.Ok(albums);
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<AlbumApiModel>>> GetAlbumsByArtistId(int id, IAlbumService service, CancellationToken ct)
    {
        var albums = await service.GetAlbumsByArtistIdAsync(id, ct);
        return TypedResults.Ok(albums);
    }
}
