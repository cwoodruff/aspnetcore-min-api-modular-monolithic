using Admin.Modules.Data;
using Admin.Modules.Domain;
using Admin.Modules.Mapping;
using Admin.Modules.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;

namespace Admin.Modules.Services;

internal sealed class EmployeeService(
    AdministrationDbContext db,
    [FromKeyedServices(AdministrationModule.ModuleName)] ICacheFacade cache,
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
            await LoadByIdAsync(id, ct)
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
            var employeeEntities = await db.Employees.AsNoTracking().ToListAsync(ct);
            return employeeEntities.ToApiModels();
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
            var employeeEntities = await LoadDirectReportsAsync(id, ct);
            return employeeEntities.ToApiModels();
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
            var m = await LoadReportsToAsync(id, ct);
            return m?.ToApiModel();
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

        var entity = model.ToEntity();
        db.Employees.Add(entity);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync(EmployeeTags[0], ct);

        return entity.ToApiModel();
    }

    public async Task<bool> UpdateEmployeeAsync(EmployeeApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        var updated = await db.Employees.AnyAsync(e => e.Id == entity.Id, ct);
        if (updated)
        {
            db.Employees.Update(entity);
            await db.SaveChangesAsync(ct);

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

    // The employee's manager; null when the employee does not exist or reports to no one.
    private async Task<Employee?> LoadReportsToAsync(int id, CancellationToken ct)
    {
        return await db.Employees
            .AsNoTracking()
            .Where(manager => db.Employees.Any(e => e.Id == id && e.ReportsTo == manager.Id))
            .SingleOrDefaultAsync(ct);
    }

    private async Task<List<Employee>> LoadDirectReportsAsync(int id, CancellationToken ct)
    {
        return await db.Employees.Where(e => e.ReportsTo == id).AsNoTracking().ToListAsync(ct);
    }

    private async Task<EmployeeApiModel?> LoadByIdAsync(int id, CancellationToken ct)
    {
        return await db.Employees
            .Where(e => e.Id == id)
            .Select(e => new EmployeeApiModel
            {
                Id = e.Id,
                FirstName = e.FirstName,
                LastName = e.LastName,
                Title = e.Title,
                ReportsTo = e.ReportsTo,
                BirthDate = e.BirthDate,
                HireDate = e.HireDate,
                Address = e.Address,
                City = e.City,
                State = e.State,
                Country = e.Country,
                PostalCode = e.PostalCode,
                Phone = e.Phone,
                Fax = e.Fax,
                Email = e.Email,
                ReportsToNavigation = e.ReportsToNavigation != null
                    ? (e.ReportsToNavigation.FirstName + " " + e.ReportsToNavigation.LastName)
                    : null,
                Customers = new List<CustomerApiModel>(), // avoid deep cycles
                InverseReportsToNavigation = new List<EmployeeApiModel>()
            })
            .AsNoTracking()
            .SingleOrDefaultAsync(ct);
    }
}
