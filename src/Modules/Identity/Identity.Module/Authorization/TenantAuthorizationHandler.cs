using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Identity.Modules.Authorization;

internal sealed partial class TenantAuthorizationHandler(
    ITenantResolutionService resolver,
    IHttpContextAccessor httpContextAccessor,
    ILogger<TenantAuthorizationHandler> logger)
    : AuthorizationHandler<TenantRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantRequirement requirement)
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null)
        {
            return Task.CompletedTask;
        }

        var userTenant = context.User.FindFirstValue("tenant");
        if (string.IsNullOrWhiteSpace(userTenant))
        {
            // OWASP A01: No tenant claim -> cannot satisfy tenant scoped resources
            LogNoTenantClaim(logger);
            return Task.CompletedTask;
        }

        var requestTenant = resolver.ResolveTenantId(http);
        if (string.IsNullOrWhiteSpace(requestTenant))
        {
            // No X-Tenant-Id header: assume the user's own tenant scope.
            // This allows Swagger/curl usage without the header while still
            // enforcing tenant isolation when the header IS provided.
            LogImplicitTenant(logger, userTenant);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (string.Equals(userTenant, requestTenant, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }
        else
        {
            // OWASP A01: Log cross-tenant access attempts
            LogTenantMismatch(logger, userTenant, requestTenant);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tenant-scoped access denied: user has no tenant claim")]
    private static partial void LogNoTenantClaim(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No X-Tenant-Id header; implicitly scoped to user tenant '{UserTenant}'")]
    private static partial void LogImplicitTenant(ILogger logger, string userTenant);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tenant mismatch: user tenant '{UserTenant}' attempted to access tenant '{RequestTenant}'")]
    private static partial void LogTenantMismatch(ILogger logger, string userTenant, string requestTenant);
}
