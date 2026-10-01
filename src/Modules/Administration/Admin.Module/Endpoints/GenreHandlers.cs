using System.ComponentModel.DataAnnotations;
using Admin.Modules.Domain;
using Admin.Modules.Models;
using Admin.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json.Serialization;

namespace Admin.Modules.Endpoints;

/// <summary>The genre endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class GenreHandlers
{
    [Authorize]
    public static async Task<Results<Ok<GenreApiModel>, NotFound>> GetGenreById(int id, IGenreService service, CancellationToken ct)
    {
        var genre = await service.GetGenreByIdAsync(id, ct);
        return genre is not null ? TypedResults.Ok(genre) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Ok<IEnumerable<GenreApiModel>>> GetAllGenres(IGenreService service, CancellationToken ct)
    {
        var genres = await service.GetAllGenresAsync(ct);
        return TypedResults.Ok(genres);
    }

    [Authorize]
    public static async Task<Created<GenreApiModel>> CreateGenre(
        GenreEndpoints.CreateGenreRequest request, IGenreService service, CancellationToken ct)
    {
        var created = await service.CreateGenreAsync(request.Name, ct);
        return TypedResults.Created($"/api/admin/genres/{created!.Id}", created);
    }

    [Authorize]
    public static async Task<Results<Ok<GenreUpdated>, NotFound>> UpdateGenre(
        int id, GenreEndpoints.UpdateGenreRequest request, IGenreService service, CancellationToken ct)
    {
        var updated = await service.UpdateGenreAsync(id, request.Name, ct);
        return updated ? TypedResults.Ok(new GenreUpdated(id, request.Name)) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Results<NoContent, NotFound>> DeleteGenre(int id, IGenreService service, CancellationToken ct)
    {
        var deleted = await service.DeleteGenreAsync(id, ct);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    /// <summary>The update response; lower-case names as before the handlers moved.</summary>
    internal sealed record GenreUpdated(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name);
}
