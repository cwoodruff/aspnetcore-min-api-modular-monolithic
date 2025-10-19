using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Identity.Modules.Authorization;

public sealed class TenantAuthorizationHandler : AuthorizationHandler<TenantRequirement>
{
    private readonly ITenantResolutionService _resolver;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantAuthorizationHandler(ITenantResolutionService resolver, IHttpContextAccessor httpContextAccessor)
    {
        _resolver = resolver;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantRequirement requirement)
    {
        var http = _httpContextAccessor.HttpContext;
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

        var requestTenant = _resolver.ResolveTenantId(http);
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
