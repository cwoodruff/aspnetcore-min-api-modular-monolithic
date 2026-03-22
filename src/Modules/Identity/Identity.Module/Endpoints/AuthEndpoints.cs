using System.Security.Claims;
using Identity.Modules.KeyManagement;
using Identity.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharedKernel.TrafficControl;

namespace Identity.Modules.Endpoints;

public static partial class AuthEndpoints
{
    public static void MapIdentityAuthEndpoints(this IEndpointRouteBuilder group)
    {
        // POST /api/identity/login
        group.MapPost("/login",
                async (LoginRequest req, IUserStore users, ITokenService tokens,
                    ILoggerFactory loggerFactory, CancellationToken ct) =>
                {
                    var logger = loggerFactory.CreateLogger("Identity.Auth");

                    // OWASP A07: Validate login input before processing
                    if (string.IsNullOrWhiteSpace(req.username) || string.IsNullOrWhiteSpace(req.password))
                    {
                        return Results.Problem(
                            title: "Invalid request",
                            detail: "Username and password are required.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    var result = await users.ValidateCredentialsAsync(req.username, req.password, ct);
                    if (!result.success)
                    {
                        // OWASP A09: Log failed authentication attempts
                        LogFailedLogin(logger, req.username);
                        return Results.Unauthorized();
                    }

                    LogSuccessfulLogin(logger, result.userId);

                    var pair = await tokens.IssueAsync(result.userId, result.displayName, result.roles,
                        result.permissions, result.email, result.tenant, ct);
                    return Results.Json(new
                    {
                        access_token = pair.AccessToken,
                        token_type = "Bearer",
                        expires_at_utc = pair.ExpiresAtUtc,
                        refresh_token = pair.RefreshToken
                    });
                })
            .AllowAnonymous()
            .WithTags("Identity")
            .WithName("IdentityLogin")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // POST /api/identity/refresh
        group.MapPost("/refresh", async (RefreshRequest req, ITokenService tokens,
                ILoggerFactory loggerFactory, CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("Identity.Auth");

                if (string.IsNullOrWhiteSpace(req.userId) || string.IsNullOrWhiteSpace(req.refreshToken))
                {
                    return Results.Problem(
                        title: "Invalid request",
                        detail: "UserId and refreshToken are required.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var pair = await tokens.RefreshAsync(req.userId, req.refreshToken, ct);
                if (pair is null)
                {
                    LogFailedRefresh(logger, req.userId);
                    return Results.Unauthorized();
                }

                return Results.Json(new
                {
                    access_token = pair.AccessToken,
                    token_type = "Bearer",
                    expires_at_utc = pair.ExpiresAtUtc,
                    refresh_token = pair.RefreshToken
                });
            })
            .AllowAnonymous()
            .WithTags("Identity")
            .WithName("IdentityRefresh")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // POST /api/identity/logout
        // OWASP A01: Verify the authenticated user owns the session being revoked
        group.MapPost("/logout", async (LogoutRequest req, ClaimsPrincipal user,
                IRefreshTokenStore store, ILoggerFactory loggerFactory, CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("Identity.Auth");

                var authenticatedUserId = user.FindFirstValue("sub")
                                          ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(authenticatedUserId) ||
                    !string.Equals(authenticatedUserId, req.userId, StringComparison.Ordinal))
                {
                    LogLogoutMismatch(logger, authenticatedUserId, req.userId);
                    return Results.Forbid();
                }

                await store.RevokeAsync(req.userId, req.refreshToken, ct);
                LogLogout(logger, req.userId);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithTags("Identity")
            .WithName("IdentityLogout")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/identity/userinfo
        group.MapGet("/userinfo", [Authorize](ClaimsPrincipal user) =>
            {
                var response = new
                {
                    sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier),
                    name = user.FindFirstValue(ClaimTypes.Name),
                    email = user.FindFirstValue(ClaimTypes.Email),
                    roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
                    permissions = user.FindAll("permissions").Select(c => c.Value).ToArray()
                };
                return Results.Json(response);
            })
            .WithTags("Identity")
            .WithName("IdentityUserInfo")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /.well-known/jwks.json
        group.MapGet("/.well-known/jwks.json", (IKeyMaterialService keys) => Results.Json(keys.GetJwksDocument()))
            .AllowAnonymous()
            .WithTags("Identity")
            .WithName("IdentityJWKS")
            .Produces(429) // Rate limiting
            .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed login attempt for user '{Username}'")]
    private static partial void LogFailedLogin(ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successful login for user '{UserId}'")]
    private static partial void LogSuccessfulLogin(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed refresh attempt for user '{UserId}'")]
    private static partial void LogFailedRefresh(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Logout ownership mismatch: authenticated '{AuthUserId}' tried to revoke tokens for '{RequestUserId}'")]
    private static partial void LogLogoutMismatch(ILogger logger, string? authUserId, string requestUserId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User '{UserId}' logged out")]
    private static partial void LogLogout(ILogger logger, string userId);

    private sealed record LoginRequest(string username, string password);

    private sealed record RefreshRequest(string userId, string refreshToken);

    private sealed record LogoutRequest(string userId, string refreshToken);
}
