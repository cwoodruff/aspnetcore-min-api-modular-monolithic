using Admin.Modules.Domain;
using Admin.Modules.Models;
using Admin.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Admin.Modules.Endpoints;

/// <summary>The employee endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class EmployeeHandlers
{
    [Authorize]
    public static async Task<Results<Ok<EmployeeApiModel>, NotFound>> GetEmployeeById(int id, IEmployeeService service, CancellationToken ct)
    {
        var employee = await service.GetEmployeeByIdAsync(id, ct);
        return employee is not null ? TypedResults.Ok(employee) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Ok<IEnumerable<EmployeeApiModel>>> GetAllEmployees(IEmployeeService service, CancellationToken ct)
    {
        var employees = await service.GetAllEmployeesAsync(ct);
        return TypedResults.Ok(employees);
    }

    [Authorize]
    public static async Task<Ok<IEnumerable<EmployeeApiModel>>> GetEmployeeDirectReports(int id, IEmployeeService service, CancellationToken ct)
    {
        var employees = await service.GetDirectReportsAsync(id, ct);
        return TypedResults.Ok(employees);
    }

    [Authorize]
    public static async Task<Results<Ok<EmployeeApiModel>, NotFound>> GetEmployeeReportsTo(int id, IEmployeeService service, CancellationToken ct)
    {
        var manager = await service.GetReportsToAsync(id, ct);
        return manager is not null ? TypedResults.Ok(manager) : TypedResults.NotFound();
    }
}
