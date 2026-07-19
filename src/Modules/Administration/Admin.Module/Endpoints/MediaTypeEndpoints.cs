using Admin.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel.TrafficControl;

namespace Admin.Modules.Endpoints;

internal static class MediaTypeEndpoints
{
    public static void MapMediaTypeEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/media-types/{id}
        group.MapGet("/media-types/{id:int}", [Authorize] async (
                int id,
                IMediaTypeService service,
                CancellationToken ct) =>
            {
                var mediaType = await service.GetMediaTypeByIdAsync(id, ct);

                return mediaType is not null ? Results.Json(mediaType) : Results.NotFound();
            })
            .RequireAdministrationReadAccess()
            .WithName("AdministrationGetMediaTypeById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/admin/media-types
        group.MapGet("media-types/", [Authorize] async (
                IMediaTypeService service,
                CancellationToken ct) =>
            {
                var mediaTypes = await service.GetAllMediaTypesAsync(ct);

                return Results.Json(mediaTypes);
            })
            .RequireAdministrationReadAccess()
            .WithName("GetAllMediaTypes")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
