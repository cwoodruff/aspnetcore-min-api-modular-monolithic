using Identity.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Modules.Authorization;

internal static class PolicyRegistry
{
    public static void Register(AuthorizationOptions options)
    {
        // Do not set a global fallback policy; endpoints remain anonymous unless marked with RequireAuthorization.
        // One policy per permission, named after it; other modules apply them by the Identity.Contracts constants.
        AddPermissionPolicy(options, Permissions.CatalogRead);
        AddPermissionPolicy(options, Permissions.CatalogWrite);
        AddPermissionPolicy(options, Permissions.OrdersRead);
        AddPermissionPolicy(options, Permissions.OrdersWrite);
        AddPermissionPolicy(options, Permissions.AdminUsersManage);
        AddPermissionPolicy(options, Permissions.AdministrationRead);
        AddPermissionPolicy(options, Permissions.AdministrationWrite);
        AddPermissionPolicy(options, Permissions.ReportView);

        // Role-based convenience policies
        options.AddPolicy(Policies.Admin, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole("Admin");
        });

        // Tenant scoped policy
        options.AddPolicy(Policies.TenantScoped, policy =>
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
