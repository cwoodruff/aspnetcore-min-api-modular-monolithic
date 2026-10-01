using Admin.Modules.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Admin.Modules.Endpoints;

internal static class MediaTypeEndpoints
{
    public static void MapMediaTypeEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/media-types/{id}
        group.MapGet("/media-types/{id:int}", MediaTypeHandlers.GetMediaTypeById)
            .RequireAdministrationReadAccess()
            .WithName("AdministrationGetMediaTypeById")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/media-types
        group.MapGet("media-types/", MediaTypeHandlers.GetAllMediaTypes)
            .RequireAdministrationReadAccess()
            .WithName("GetAllMediaTypes")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy
    }
}
