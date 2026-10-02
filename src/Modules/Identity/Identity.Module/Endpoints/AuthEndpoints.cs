using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel.Validation;

namespace Identity.Modules.Endpoints;

internal static class AuthEndpoints
{
    public static void MapIdentityAuthEndpoints(this IEndpointRouteBuilder group)
    {
        // POST /api/identity/login
        group.MapPost("/login", AuthHandlers.Login)
            .AddEndpointFilter<ValidationFilter<AuthHandlers.LoginRequest>>()
            .Produces(StatusCodes.Status400BadRequest)
            .AllowAnonymous()
            .WithTags("Identity")
            .WithName("IdentityLogin")
            .Produces(429); // Rate limited by the module group's policy

        // POST /api/identity/refresh
        group.MapPost("/refresh", AuthHandlers.Refresh)
            .AddEndpointFilter<ValidationFilter<AuthHandlers.RefreshRequest>>()
            .Produces(StatusCodes.Status400BadRequest)
            .AllowAnonymous()
            .WithTags("Identity")
            .WithName("IdentityRefresh")
            .Produces(429); // Rate limited by the module group's policy

        // POST /api/identity/logout
        group.MapPost("/logout", AuthHandlers.Logout)
            .AddEndpointFilter<ValidationFilter<AuthHandlers.LogoutRequest>>()
            .Produces(StatusCodes.Status400BadRequest)
            .RequireAuthorization()
            .WithTags("Identity")
            .WithName("IdentityLogout")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/identity/userinfo
        group.MapGet("/userinfo", AuthHandlers.UserInfo)
            .WithTags("Identity")
            .WithName("IdentityUserInfo")
            .Produces(429); // Rate limited by the module group's policy

        // GET /.well-known/jwks.json
        group.MapGet("/.well-known/jwks.json", AuthHandlers.Jwks)
            .AllowAnonymous()
            .WithTags("Identity")
            .WithName("IdentityJWKS")
            .Produces(429); // Rate limited by the module group's policy
    }
}
