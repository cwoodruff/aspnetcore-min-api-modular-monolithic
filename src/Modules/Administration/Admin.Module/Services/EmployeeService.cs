using FluentValidation;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Services;

public sealed class EmployeeService(
    IEmployeeRepository repo,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<EmployeeApiModel> validator,
    ILogger<EmployeeService> logger) : IEmployeeService
{
    private static readonly string[] EmployeeTags = ["administration:employee", "administration:employee:by-id"];
    private readonly ILogger<EmployeeService> _logger = logger;
    private readonly IValidator<EmployeeApiModel> _validator = validator;

    public async Task<EmployeeApiModel?> GetEmployeeByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "employee",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<EmployeeApiModel?>(key, async _ =>
            await repo.GetById(id)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = EmployeeTags
        }, ct);
    }

    public async Task<IEnumerable<EmployeeApiModel>> GetAllEmployeesAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "employee",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<EmployeeApiModel>>(key, async _ =>
        {
            var employeeEntities = await repo.GetAll();
            return employeeEntities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = EmployeeTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<EmployeeApiModel>> GetDirectReportsAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "employee",
            "v1",
            $"direct-reports:{id}");

        return await cache.GetOrAddAsync<IEnumerable<EmployeeApiModel>>(key, async _ =>
        {
            var employeeEntities = await repo.GetDirectReports(id);
            return employeeEntities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = EmployeeTags
        }, ct) ?? [];
    }

    public async Task<EmployeeApiModel?> GetReportsToAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "employee",
            "v1",
            $"reports-to:{id}");

        return await cache.GetOrAddAsync<EmployeeApiModel?>(key, async _ =>
        {
            var m = await repo.GetReportsTo(id);
            return m?.Convert();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = EmployeeTags
        }, ct);
    }

    public async Task<EmployeeApiModel?> CreateEmployeeAsync(EmployeeApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var created = await repo.Add(entity);

        // Invalidate cache
        await cache.RemoveByTagAsync(EmployeeTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdateEmployeeAsync(EmployeeApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var updated = await repo.Update(entity);

        if (updated)
        {
            // Invalidate cache
            await cache.RemoveByTagAsync(EmployeeTags[0], ct);
            var key = keys.Compose(
                "administration",
                "employee",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
