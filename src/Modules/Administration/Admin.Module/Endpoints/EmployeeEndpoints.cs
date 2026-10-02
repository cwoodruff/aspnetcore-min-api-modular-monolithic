using Admin.Modules.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Admin.Modules.Endpoints;

internal static class EmployeeEndpoints
{
    public static void MapEmployeeEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/employees/{id}
        group.MapGet("/employees/{id:int}", EmployeeHandlers.GetEmployeeById)
            .RequireAdministrationReadAccess()
            .WithName("AdministrationGetEmployeeById")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/employees
        group.MapGet("employees/", EmployeeHandlers.GetAllEmployees)
            .RequireAdministrationReadAccess()
            .WithName("GetAllEmployees")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/employees/{id}/direct-reports
        group.MapGet("employees/{id:int}/direct-reports", EmployeeHandlers.GetEmployeeDirectReports)
            .RequireAdministrationReadAccess()
            .WithName("GetEmployeeDirectReports")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/admin/employees/{id}/reports-to
        group.MapGet("employees/{id:int}/reports-to", EmployeeHandlers.GetEmployeeReportsTo)
            .RequireAdministrationReadAccess()
            .WithName("GetEmployeeReportsTo")
            .WithDescription("The employee's manager. 404 when the employee does not exist or reports to no one.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Administration")
            .Produces(429); // Rate limited by the module group's policy
    }
}
