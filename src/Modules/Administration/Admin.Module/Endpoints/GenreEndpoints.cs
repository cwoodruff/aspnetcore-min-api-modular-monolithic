using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;
using SharedKernel.Persistence;

namespace Admin.Modules.Endpoints;

public static class GenreEndpoints
{
    private static readonly string[] GenreTags = ["administration:genre", "administration:genre:by-id"];

    public static void MapGenreEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/administration/genres/{id}
        group.MapGet("/genres/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
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
                        var g = await db.GetGenre(id);
                        if (g is null) return null;
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
            .WithTags("Administration");

        // GET /api/administration/genres
        group.MapGet("genres/", [Authorize] async (
                AppDbContext db,
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
                        var genreEntities = db.GetAllGenres();
                        return [genreEntities.Select(g => g.Convert())];
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
            .WithTags("Administration");
    }
}
