using Catalog.Modules.Domain;
using Catalog.Modules.Models;
using Catalog.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Modules.Endpoints;

/// <summary>The playlist endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class PlaylistHandlers
{
    [Authorize]
    public static async Task<Results<Ok<PlaylistApiModel>, NotFound>> GetPlaylistById(int id, IPlaylistService service, CancellationToken ct)
    {
        var playlist = await service.GetPlaylistByIdAsync(id, ct);
        return playlist is not null ? TypedResults.Ok(playlist) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Ok<IReadOnlyList<PlaylistApiModel>>> GetAllPlaylists(IPlaylistService service, CancellationToken ct)
    {
        var playlists = await service.GetAllPlaylistsAsync(ct);
        return TypedResults.Ok(playlists);
    }
}
