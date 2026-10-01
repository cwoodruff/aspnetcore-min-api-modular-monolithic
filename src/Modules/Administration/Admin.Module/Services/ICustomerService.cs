using Admin.Modules.Models;

namespace Admin.Modules.Services;

internal interface ICustomerService
{
    Task<CustomerApiModel?> GetCustomerByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<CustomerApiModel>> GetAllCustomersAsync(CancellationToken ct);
    Task<IEnumerable<CustomerApiModel>> GetCustomersBySupportRepIdAsync(int id, CancellationToken ct);
    Task<CustomerApiModel?> CreateCustomerAsync(CustomerApiModel model, CancellationToken ct);
    Task<bool> UpdateCustomerAsync(CustomerApiModel model, CancellationToken ct);
    Task<CustomerPurchasesApiModel?> GetCustomerPurchasesAsync(int id, CancellationToken ct);
}
