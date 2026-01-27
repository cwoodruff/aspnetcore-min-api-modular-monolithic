using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel.Caching;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Endpoints;

public static class GenreEndpoints
{
    private static readonly string[] GenreTags = ["administration:genre", "administration:genre:by-id"];

    // Request DTOs with validation
    public record CreateGenreRequest([Required, StringLength(120, MinimumLength = 1)] string Name);
    public record UpdateGenreRequest([Required, StringLength(120, MinimumLength = 1)] string Name);

    public static void MapGenreEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/genres/{id}
        group.MapGet("/genres/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                IGenreRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "genre",
                    version: "v1",
                    discriminator: $"by-id:{id}");

                var genre = await cache.GetOrAddAsync<object?>(key, async _ =>
                {
                    try
                    {
                        var g = await repo.GetById(id);
                        return g.Convert();
                    }
                    catch
                    {
                        return null;
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = GenreTags
                }, ct);

                return genre is not null ? Results.Json(genre) : Results.NotFound();
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("AdministrationGetGenreById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/admin/genres
        group.MapGet("genres/", [Authorize] async (
                AppDbContext db,
                IGenreRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "genre",
                    version: "v1",
                    discriminator: "all");

                var genres = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var genreEntities = await repo.GetAll();
                        return genreEntities.ConvertAll();
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = GenreTags
                }, ct);

                return Results.Json(genres);
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllGenres")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // POST /api/admin/genres
        group.MapPost("/genres", [Authorize] async (
                CreateGenreRequest request,
                IGenreRepository repo,
                ICacheFacade cache,
                CancellationToken ct) =>
            {
                // Validate request
                var validationResults = new List<ValidationResult>();
                if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
                {
                    return Results.ValidationProblem(
                        validationResults.ToDictionary(
                            v => v.MemberNames.FirstOrDefault() ?? "error",
                            v => new[] { v.ErrorMessage ?? "Validation error" }));
                }

                var genre = new Genre { Name = request.Name };
                var created = await repo.Add(genre);

                // Invalidate cache for genre lists
                await cache.RemoveByTagAsync("administration:genre", ct);

                return Results.Created($"/api/admin/genres/{created.Id}", new { id = created.Id, name = created.Name });
            })
            .RequireAuthorization("administration.write").RequireAuthorization("tenant.scoped")
            .WithName("CreateGenre")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429)
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // PUT /api/admin/genres/{id}
        group.MapPut("/genres/{id:int}", [Authorize] async (
                int id,
                UpdateGenreRequest request,
                IGenreRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Validate request
                var validationResults = new List<ValidationResult>();
                if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
                {
                    return Results.ValidationProblem(
                        validationResults.ToDictionary(
                            v => v.MemberNames.FirstOrDefault() ?? "error",
                            v => new[] { v.ErrorMessage ?? "Validation error" }));
                }

                // Check if exists
                if (!await repo.EntityExists(id))
                {
                    return Results.NotFound();
                }

                var genre = new Genre { Id = id, Name = request.Name };
                var updated = await repo.Update(genre);

                if (!updated)
                {
                    return Results.Problem("Failed to update genre", statusCode: StatusCodes.Status500InternalServerError);
                }

                // Invalidate cache for this genre and lists
                var key = keys.Compose("administration", "genre", "v1", discriminator: $"by-id:{id}");
                await cache.RemoveAsync(key, ct);
                await cache.RemoveByTagAsync("administration:genre", ct);

                return Results.Ok(new { id, name = request.Name });
            })
            .RequireAuthorization("administration.write").RequireAuthorization("tenant.scoped")
            .WithName("UpdateGenre")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429)
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // DELETE /api/admin/genres/{id}
        group.MapDelete("/genres/{id:int}", [Authorize] async (
                int id,
                IGenreRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                // Check if exists
                if (!await repo.EntityExists(id))
                {
                    return Results.NotFound();
                }

                var deleted = await repo.Delete(id);

                if (!deleted)
                {
                    return Results.Problem("Failed to delete genre", statusCode: StatusCodes.Status500InternalServerError);
                }

                // Invalidate cache
                var key = keys.Compose("administration", "genre", "v1", discriminator: $"by-id:{id}");
                await cache.RemoveAsync(key, ct);
                await cache.RemoveByTagAsync("administration:genre", ct);

                return Results.NoContent();
            })
            .RequireAuthorization("administration.write").RequireAuthorization("tenant.scoped")
            .WithName("DeleteGenre")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429)
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
