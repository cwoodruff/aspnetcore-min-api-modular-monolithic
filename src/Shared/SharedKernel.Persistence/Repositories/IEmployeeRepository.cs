using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.Repositories;

public interface IEmployeeRepository : IRepository<Employee>, IDisposable
{
    Task<Employee> GetReportsTo(int id);
    Task<List<Employee>> GetDirectReports(int id);

    Task<EmployeeApiModel?> GetById(int id);
}
