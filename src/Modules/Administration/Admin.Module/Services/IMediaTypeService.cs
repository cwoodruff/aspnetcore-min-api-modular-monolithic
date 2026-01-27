using SharedKernel.Persistence.ApiModels;

namespace Admin.Modules.Services;

public interface IMediaTypeService
{
    Task<MediaTypeApiModel?> GetMediaTypeByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<MediaTypeApiModel>> GetAllMediaTypesAsync(CancellationToken ct);
}
