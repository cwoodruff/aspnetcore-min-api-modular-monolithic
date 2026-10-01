using Admin.Modules.Domain;
using Admin.Modules.Models;
using Admin.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Admin.Modules.Endpoints;

/// <summary>The mediatype endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class MediaTypeHandlers
{
    [Authorize]
    public static async Task<Results<Ok<MediaTypeApiModel>, NotFound>> GetMediaTypeById(int id, IMediaTypeService service, CancellationToken ct)
    {
        var mediaType = await service.GetMediaTypeByIdAsync(id, ct);
        return mediaType is not null ? TypedResults.Ok(mediaType) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Ok<IEnumerable<MediaTypeApiModel>>> GetAllMediaTypes(IMediaTypeService service, CancellationToken ct)
    {
        var mediaTypes = await service.GetAllMediaTypesAsync(ct);
        return TypedResults.Ok(mediaTypes);
    }
}
