using Identity.Contracts;
using Microsoft.AspNetCore.Builder;

namespace Admin.Modules.Endpoints;

internal static class AuthorizationConventions
{
    public static TBuilder RequireAdministrationReadAccess<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder
            .RequireAuthorization(Policies.Admin)
            .RequireAuthorization(Permissions.AdministrationRead)
            .RequireAuthorization(Policies.TenantScoped);
    }

    public static TBuilder RequireAdministrationWriteAccess<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder
            .RequireAuthorization(Policies.Admin)
            .RequireAuthorization(Permissions.AdministrationWrite)
            .RequireAuthorization(Policies.TenantScoped);
    }
}
