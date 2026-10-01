using System.ComponentModel.DataAnnotations;
using Admin.Modules.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Admin.Modules.Endpoints;

internal static class GenreEndpoints
{
    public static void MapGenreEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/genres/{id}
        group.MapGet("/genres/{id:int}", GenreHandlers.GetGenreById)
            .RequireAdministrationReadAccess()
            .WithName("AdministrationGetGenreById")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/genres
        group.MapGet("genres/", GenreHandlers.GetAllGenres)
            .RequireAdministrationReadAccess()
            .WithName("GetAllGenres")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // POST /api/admin/genres
        group.MapPost("/genres", GenreHandlers.CreateGenre)
            .RequireAdministrationWriteAccess()
            .WithName("CreateGenre")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429);

        // PUT /api/admin/genres/{id}
        group.MapPut("/genres/{id:int}", GenreHandlers.UpdateGenre)
            .RequireAdministrationWriteAccess()
            .WithName("UpdateGenre")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429);

        // DELETE /api/admin/genres/{id}
        group.MapDelete("/genres/{id:int}", GenreHandlers.DeleteGenre)
            .RequireAdministrationWriteAccess()
            .WithName("DeleteGenre")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429);
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
