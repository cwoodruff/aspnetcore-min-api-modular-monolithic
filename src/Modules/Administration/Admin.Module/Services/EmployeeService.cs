using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Services;

public sealed class EmployeeService(
    IEmployeeRepository repo,
    ICacheFacade cache,
    ICacheKeyComposer keys) : IEmployeeService
{
    private static readonly string[] EmployeeTags = ["administration:employee", "administration:employee:by-id"];

    public async Task<EmployeeApiModel?> GetEmployeeByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "employee",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<EmployeeApiModel?>(key, async _ =>
        {
            try
            {
                var e = await repo.GetById(id);
                return e;
            }
            catch
            {
                return null;
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = EmployeeTags
        }, ct);
    }

    public async Task<IEnumerable<EmployeeApiModel>> GetAllEmployeesAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "employee",
            version: "v1",
            discriminator: "all");

        return await cache.GetOrAddAsync<IEnumerable<EmployeeApiModel>>(key, async _ =>
        {
            try
            {
                var employeeEntities = await repo.GetAll();
                return employeeEntities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = EmployeeTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<EmployeeApiModel>> GetDirectReportsAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "employee",
            version: "v1",
            discriminator: $"direct-reports:{id}");

        return await cache.GetOrAddAsync<IEnumerable<EmployeeApiModel>>(key, async _ =>
        {
            try
            {
                var employeeEntities = await repo.GetDirectReports(id);
                return employeeEntities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = EmployeeTags
        }, ct) ?? [];
    }

    public async Task<EmployeeApiModel?> GetReportsToAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "employee",
            version: "v1",
            discriminator: $"reports-to:{id}");

        return await cache.GetOrAddAsync<EmployeeApiModel?>(key, async _ =>
        {
            try
            {
                var m = await repo.GetReportsTo(id);
                return m.Convert();
            }
            catch
            {
                return null;
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = EmployeeTags
        }, ct);
    }
}
