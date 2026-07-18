using Microsoft.AspNetCore.Authorization;

namespace Identity.Modules.Authorization;

internal sealed class TenantRequirement : IAuthorizationRequirement
{
    public static readonly TenantRequirement Instance = new();

    private TenantRequirement()
    {
    }
}
