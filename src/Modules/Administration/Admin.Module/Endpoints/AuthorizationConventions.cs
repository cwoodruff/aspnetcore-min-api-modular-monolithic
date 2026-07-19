using Microsoft.AspNetCore.Builder;

namespace Admin.Modules.Endpoints;

internal static class AuthorizationConventions
{
    private const string AdminRolePolicy = "role.admin";
    private const string AdministrationReadPolicy = "administration.read";
    private const string AdministrationWritePolicy = "administration.write";
    private const string TenantScopedPolicy = "tenant.scoped";

    public static TBuilder RequireAdministrationReadAccess<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder
            .RequireAuthorization(AdminRolePolicy)
            .RequireAuthorization(AdministrationReadPolicy)
            .RequireAuthorization(TenantScopedPolicy);
    }

    public static TBuilder RequireAdministrationWriteAccess<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder
            .RequireAuthorization(AdminRolePolicy)
            .RequireAuthorization(AdministrationWritePolicy)
            .RequireAuthorization(TenantScopedPolicy);
    }
}
