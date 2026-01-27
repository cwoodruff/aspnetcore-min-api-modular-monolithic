using SharedKernel.Persistence.ApiModels;

namespace Admin.Modules.Services;

public interface IEmployeeService
{
    Task<EmployeeApiModel?> GetEmployeeByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<EmployeeApiModel>> GetAllEmployeesAsync(CancellationToken ct);
    Task<IEnumerable<EmployeeApiModel>> GetDirectReportsAsync(int id, CancellationToken ct);
    Task<EmployeeApiModel?> GetReportsToAsync(int id, CancellationToken ct);
}
