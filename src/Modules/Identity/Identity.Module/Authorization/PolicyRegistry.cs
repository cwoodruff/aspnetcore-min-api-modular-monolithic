using Microsoft.AspNetCore.Authorization;

namespace Identity.Modules.Authorization;

internal static class PolicyRegistry
{
    public const string AdminPolicy = "role.admin";
    public const string TenantScopedPolicy = "tenant.scoped";

    public static void Register(AuthorizationOptions options)
    {
        // Do not set a global fallback policy; endpoints remain anonymous unless marked with RequireAuthorization.
        // Permission-based policies (other modules can refer by string name)
        AddPermissionPolicy(options, Permissions.MusicRead);
        AddPermissionPolicy(options, Permissions.MusicWrite);
        AddPermissionPolicy(options, Permissions.OrdersRead);
        AddPermissionPolicy(options, Permissions.OrdersWrite);
        AddPermissionPolicy(options, Permissions.AdminUsersManage);
        AddPermissionPolicy(options, Permissions.AdministrationRead);
        AddPermissionPolicy(options, Permissions.AdministrationWrite);
        AddPermissionPolicy(options, Permissions.ReportView);

        // Role-based convenience policies
        options.AddPolicy(AdminPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole("Admin");
        });

        // Tenant scoped policy
        options.AddPolicy(TenantScopedPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(TenantRequirement.Instance);
        });
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
