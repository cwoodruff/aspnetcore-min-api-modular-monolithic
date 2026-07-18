using Microsoft.AspNetCore.Http;

namespace Identity.Modules.Authorization;

internal interface ITenantResolutionService
{
    // Returns the tenant identifier for the current request if present.
    // Resolution order: route values (tenant or tenantId) -> header X-Tenant-Id -> null if none.
    string? ResolveTenantId(HttpContext httpContext);
}
