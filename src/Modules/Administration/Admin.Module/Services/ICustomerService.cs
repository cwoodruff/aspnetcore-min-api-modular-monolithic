using SharedKernel.Persistence.ApiModels;

namespace Admin.Modules.Services;

public interface ICustomerService
{
    Task<CustomerApiModel?> GetCustomerByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<CustomerApiModel>> GetAllCustomersAsync(CancellationToken ct);
    Task<IEnumerable<CustomerApiModel>> GetCustomersBySupportRepIdAsync(int id, CancellationToken ct);
}
