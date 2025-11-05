using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel.Caching;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Endpoints;

public static class MediaTypeEndpoints
{
    private static readonly string[] MediaTypeTags = ["administration:mediatype", "administration:mediatype:by-id"];

    public static void MapMediaTypeEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/media-types/{id}
        group.MapGet("/media-types/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                IMediaTypeRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "mediatype",
                    version: "v1",
                    discriminator: $"by-id:{id}");

                var mediaType = await cache.GetOrAddAsync<object?>(key, async _ =>
                {
                    try
                    {
                        var m = await repo.GetById(id);
                        return m.Convert();
                    }
                    catch
                    {
                        return null;
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = MediaTypeTags
                }, ct);

                return mediaType is not null ? Results.Json(mediaType) : Results.NotFound();
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("AdministrationGetMediaTypeById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/admin/media-types
        group.MapGet("media-types/", [Authorize] async (
                AppDbContext db,
                IMediaTypeRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "mediatype",
                    version: "v1",
                    discriminator: "all");

                var mediaTypes = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
                {
                    try
                    {
                        await Task.Yield();
                        var mediaTypeEntities = await repo.GetAll();
                        return mediaTypeEntities.ConvertAll();
                    }
                    catch
                    {
                        return [];
                    }
                }, new CacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                    Tags = MediaTypeTags
                }, ct);

                return Results.Json(mediaTypes);
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllMediaTypes")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
