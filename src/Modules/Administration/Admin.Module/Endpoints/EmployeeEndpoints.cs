using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel.Caching;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Endpoints;

public static class EmployeeEndpoints
{
    private static readonly string[] EmployeeTags = ["administration:employee", "administration:employee:by-id"];

    public static void MapEmployeeEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/employees/{id}
        group.MapGet("/employees/{id:int}", [Authorize] async (
                int id,
                AppDbContext db,
                IEmployeeRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "employee",
                    version: "v1",
                    discriminator: $"by-id:{id}");

                var employee = await cache.GetOrAddAsync<object?>(key, async _ =>
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
                AppDbContext db,
                IEmployeeRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "employee",
                    version: "v1",
                    discriminator: "all");

                var employees = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
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
                }, ct);

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
                AppDbContext db,
                IEmployeeRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "employee",
                    version: "v1",
                    discriminator: $"direct-reports:{id}");

                var employees = await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
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
                }, ct);

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
                AppDbContext db,
                IEmployeeRepository repo,
                ICacheFacade cache,
                ICacheKeyComposer keys,
                CancellationToken ct) =>
            {
                var key = keys.Compose(
                    moduleName: "administration",
                    entity: "employee",
                    version: "v1",
                    discriminator: $"reports-to:{id}");

                var manager = await cache.GetOrAddAsync<object?>(key, async _ =>
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
