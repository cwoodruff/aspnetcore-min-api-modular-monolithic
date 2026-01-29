using Microsoft.AspNetCore.Http;

namespace Identity.Modules.Authorization;

public sealed class HttpContextTenantResolutionService : ITenantResolutionService
{
    private const string HeaderName = "X-Tenant-Id";

    public string? ResolveTenantId(HttpContext httpContext)
    {
        // Try route values first
        if (httpContext.Request.RouteValues.TryGetValue("tenant", out var t) && t is string s1 &&
            !string.IsNullOrWhiteSpace(s1))
        {
            return s1;
        }

        if (httpContext.Request.RouteValues.TryGetValue("tenantId", out var t2) && t2 is string s2 &&
            !string.IsNullOrWhiteSpace(s2))
        {
            return s2;
        }

        // Then header
        if (httpContext.Request.Headers.TryGetValue(HeaderName, out var header) && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return null;
    }
}
