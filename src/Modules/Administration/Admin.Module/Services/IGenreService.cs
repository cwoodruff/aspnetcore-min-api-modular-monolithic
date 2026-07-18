using SharedKernel.Persistence.ApiModels;

namespace Admin.Modules.Services;

internal interface IGenreService
{
    Task<GenreApiModel?> GetGenreByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<GenreApiModel>> GetAllGenresAsync(CancellationToken ct);
    Task<GenreApiModel?> CreateGenreAsync(string name, CancellationToken ct);
    Task<bool> UpdateGenreAsync(int id, string name, CancellationToken ct);
    Task<bool> DeleteGenreAsync(int id, CancellationToken ct);
}
