using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Identity.Modules.Authorization;

public sealed class TenantAuthorizationHandler(
    ITenantResolutionService resolver,
    IHttpContextAccessor httpContextAccessor)
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
            // No tenant claim -> cannot satisfy tenant scoped resources
            return Task.CompletedTask;
        }

        var requestTenant = resolver.ResolveTenantId(http);
        if (string.IsNullOrWhiteSpace(requestTenant))
        {
            // If request does not specify tenant, assume the user's own tenant scope
            // passes, enabling user-scoped queries without explicit tenant hint.
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (string.Equals(userTenant, requestTenant, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
