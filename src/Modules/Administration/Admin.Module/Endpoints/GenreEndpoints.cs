using System.ComponentModel.DataAnnotations;
using Admin.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel.TrafficControl;

namespace Admin.Modules.Endpoints;

internal static class GenreEndpoints
{
    public static void MapGenreEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/genres/{id}
        group.MapGet("/genres/{id:int}", [Authorize] async (
                int id,
                IGenreService service,
                CancellationToken ct) =>
            {
                var genre = await service.GetGenreByIdAsync(id, ct);

                return genre is not null ? Results.Json(genre) : Results.NotFound();
            })
            .RequireAdministrationReadAccess()
            .WithName("AdministrationGetGenreById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/admin/genres
        group.MapGet("genres/", [Authorize] async (
                IGenreService service,
                CancellationToken ct) =>
            {
                var genres = await service.GetAllGenresAsync(ct);

                return Results.Json(genres);
            })
            .RequireAdministrationReadAccess()
            .WithName("GetAllGenres")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // POST /api/admin/genres
        group.MapPost("/genres", [Authorize] async (
                CreateGenreRequest request,
                IGenreService service,
                CancellationToken ct) =>
            {
                var created = await service.CreateGenreAsync(request.Name, ct);

                return Results.Created($"/api/admin/genres/{created!.Id}", created);
            })
            .RequireAdministrationWriteAccess()
            .WithName("CreateGenre")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429)
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // PUT /api/admin/genres/{id}
        group.MapPut("/genres/{id:int}", [Authorize] async (
                int id,
                UpdateGenreRequest request,
                IGenreService service,
                CancellationToken ct) =>
            {
                var updated = await service.UpdateGenreAsync(id, request.Name, ct);

                return updated ? Results.Ok(new { id, name = request.Name }) : Results.NotFound();
            })
            .RequireAdministrationWriteAccess()
            .WithName("UpdateGenre")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429)
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // DELETE /api/admin/genres/{id}
        group.MapDelete("/genres/{id:int}", [Authorize] async (
                int id,
                IGenreService service,
                CancellationToken ct) =>
            {
                var deleted = await service.DeleteGenreAsync(id, ct);

                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .RequireAdministrationWriteAccess()
            .WithName("DeleteGenre")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429)
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }

    // Request DTOs with validation
    public record CreateGenreRequest(
        [Required]
        [StringLength(120, MinimumLength = 1)]
        string Name);

    public record UpdateGenreRequest(
        [Required]
        [StringLength(120, MinimumLength = 1)]
        string Name);
}
