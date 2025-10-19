using Microsoft.AspNetCore.Http;

namespace Identity.Modules.Authorization;

public interface ITenantResolutionService
{
    // Returns the tenant identifier for the current request if present.
    // Resolution order: route values (tenant or tenantId) -> header X-Tenant-Id -> null if none.
    string? ResolveTenantId(HttpContext httpContext);
}
