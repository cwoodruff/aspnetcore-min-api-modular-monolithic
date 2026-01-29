using Microsoft.AspNetCore.Authorization;

namespace Identity.Modules.Authorization;

public sealed class TenantRequirement : IAuthorizationRequirement
{
    public static readonly TenantRequirement Instance = new();

    private TenantRequirement()
    {
    }
}
