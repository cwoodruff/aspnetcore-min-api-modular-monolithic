using Microsoft.AspNetCore.Authorization;

namespace Identity.Modules.Authorization;

public static class PolicyRegistry
{
    public static void Register(AuthorizationOptions options)
    {
        // Do not set a global fallback policy; endpoints remain anonymous unless marked with RequireAuthorization.
        // Permission-based policies (other modules can refer by string name)
        AddPermissionPolicy(options, Permissions.MusicRead);
        AddPermissionPolicy(options, Permissions.MusicWrite);
        AddPermissionPolicy(options, Permissions.OrdersRead);
        AddPermissionPolicy(options, Permissions.OrdersWrite);
        AddPermissionPolicy(options, Permissions.AdminUsersManage);
        AddPermissionPolicy(options, Permissions.ReportView);
    }

    private static void AddPermissionPolicy(AuthorizationOptions options, string permission)
    {
        options.AddPolicy(permission, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("permissions", permission);
        });
    }
}
