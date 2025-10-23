using System.Security.Claims;
using Identity.Modules.KeyManagement;
using Identity.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Modules.Endpoints;

public static class AuthEndpoints
{
    public static void MapIdentityAuthEndpoints(this IEndpointRouteBuilder group)
    {
        // POST /api/identity/login
        group.MapPost("/login", async (LoginRequest req, IUserStore users, ITokenService tokens, CancellationToken ct) =>
        {
            var result = await users.ValidateCredentialsAsync(req.username, req.password, ct);
            if (!result.success)
            {
                return Results.Unauthorized();
            }

            var pair = await tokens.IssueAsync(result.userId, result.displayName, result.roles, result.permissions, result.email, result.tenant, ct);
            return Results.Json(new
            {
                access_token = pair.AccessToken,
                token_type = "Bearer",
                expires_at_utc = pair.ExpiresAtUtc,
                refresh_token = pair.RefreshToken,
            });
        })
        .AllowAnonymous()
        .WithTags("Identity")
        .WithName("IdentityLogin");

        // POST /api/identity/refresh
        group.MapPost("/refresh", async (RefreshRequest req, ITokenService tokens, CancellationToken ct) =>
        {
            var pair = await tokens.RefreshAsync(req.userId, req.refreshToken, ct);
            if (pair is null) return Results.Unauthorized();
            return Results.Json(new
            {
                access_token = pair.AccessToken,
                token_type = "Bearer",
                expires_at_utc = pair.ExpiresAtUtc,
                refresh_token = pair.RefreshToken,
            });
        })
        .AllowAnonymous()
        .WithTags("Identity")
        .WithName("IdentityRefresh");

        // POST /api/identity/logout
        group.MapPost("/logout", async (LogoutRequest req, IRefreshTokenStore store, CancellationToken ct) =>
        {
            await store.RevokeAsync(req.userId, req.refreshToken, ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithTags("Identity")
        .WithName("IdentityLogout");

        // GET /api/identity/userinfo
        group.MapGet("/userinfo", [Authorize] (ClaimsPrincipal user) =>
        {
            var response = new
            {
                sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier),
                name = user.FindFirstValue(ClaimTypes.Name),
                email = user.FindFirstValue(ClaimTypes.Email),
                roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
                permissions = user.FindAll("permissions").Select(c => c.Value).ToArray(),
            };
            return Results.Json(response);
        })
        .WithTags("Identity")
        .WithName("IdentityUserInfo");

        // GET /.well-known/jwks.json
        group.MapGet("/.well-known/jwks.json", (IKeyMaterialService keys) => Results.Json(keys.GetJwksDocument()))
            .AllowAnonymous()
            .WithTags("Identity")
            .WithName("IdentityJWKS");
    }

    public sealed record LoginRequest(string username, string password);
    public sealed record RefreshRequest(string userId, string refreshToken);
    public sealed record LogoutRequest(string userId, string refreshToken);
}
