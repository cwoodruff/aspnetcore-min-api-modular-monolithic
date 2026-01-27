using Admin.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Endpoints;

public static class EmployeeEndpoints
{
    public static void MapEmployeeEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/employees/{id}
        group.MapGet("/employees/{id:int}", [Authorize] async (
                int id,
                IEmployeeService service,
                CancellationToken ct) =>
            {
                var employee = await service.GetEmployeeByIdAsync(id, ct);

                return employee is not null ? Results.Json(employee) : Results.NotFound();
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("AdministrationGetEmployeeById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/admin/employees
        group.MapGet("employees/", [Authorize] async (
                IEmployeeService service,
                CancellationToken ct) =>
            {
                var employees = await service.GetAllEmployeesAsync(ct);

                return Results.Json(employees);
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllEmployees")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/admin/employees/{id}/direct-reports
        group.MapGet("employees/{id:int}/direct-reports", [Authorize] async (
                int id,
                IEmployeeService service,
                CancellationToken ct) =>
            {
                var employees = await service.GetDirectReportsAsync(id, ct);

                return Results.Json(employees);
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("GetEmployeeDirectReports")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/admin/employees/{id}/reports-to
        group.MapGet("employees/{id:int}/reports-to", [Authorize] async (
                int id,
                IEmployeeService service,
                CancellationToken ct) =>
            {
                var manager = await service.GetReportsToAsync(id, ct);

                return manager is not null ? Results.Json(manager) : Results.NotFound();
            })
            .RequireAuthorization("administration.read").RequireAuthorization("tenant.scoped")
            .WithName("GetEmployeeReportsTo")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
