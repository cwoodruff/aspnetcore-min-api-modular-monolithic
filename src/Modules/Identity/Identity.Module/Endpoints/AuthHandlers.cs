using System.Security.Claims;
using System.Text.Json.Serialization;
using Identity.Modules.KeyManagement;
using Identity.Modules.Services;
using Identity.Modules.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using SharedKernel.Validation;

namespace Identity.Modules.Endpoints;

/// <summary>The identity endpoints' handlers: static, typed results, testable without a host.</summary>
internal static partial class AuthHandlers
{
    public static async Task<Results<Ok<TokenResponse>, ValidationProblem, UnauthorizedHttpResult>> Login(
        LoginRequest req, IUserStore users, ITokenService tokens, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Identity.Auth");

        // OWASP A07: Validate login input before processing. ValidationFilter rejects a blank body first; this is
        // the fallback for a direct call, with the same shape.
        var errors = AuthRequestValidators.Errors(("username", req.Username), ("password", req.Password));
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var result = await users.ValidateCredentialsAsync(req.Username, req.Password, ct);
        if (!result.success)
        {
            // OWASP A09: Log failed authentication attempts
            LogFailedLogin(logger, req.Username);
            return TypedResults.Unauthorized();
        }

        LogSuccessfulLogin(logger, result.userId);

        var pair = await tokens.IssueAsync(result.userId, result.displayName, result.roles,
            result.permissions, result.email, result.tenant, ct);
        return TypedResults.Ok(TokenResponse.From(pair));
    }

    public static async Task<Results<Ok<TokenResponse>, ValidationProblem, UnauthorizedHttpResult>> Refresh(
        RefreshRequest req, ITokenService tokens, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Identity.Auth");

        var errors = AuthRequestValidators.Errors(("userId", req.UserId), ("refreshToken", req.RefreshToken));
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var pair = await tokens.RefreshAsync(req.UserId, req.RefreshToken, ct);
        if (pair is null)
        {
            LogFailedRefresh(logger, req.UserId);
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(TokenResponse.From(pair));
    }

    // OWASP A01: Verify the authenticated user owns the session being revoked
    public static async Task<Results<NoContent, ForbidHttpResult>> Logout(
        LogoutRequest req, ClaimsPrincipal user, IRefreshTokenStore store, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Identity.Auth");

        var authenticatedUserId = user.FindFirstValue("sub")
                                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(authenticatedUserId) ||
            !string.Equals(authenticatedUserId, req.UserId, StringComparison.Ordinal))
        {
            LogLogoutMismatch(logger, authenticatedUserId, req.UserId);
            return TypedResults.Forbid();
        }

        await store.RevokeAsync(req.UserId, req.RefreshToken, ct);
        LogLogout(logger, req.UserId);
        return TypedResults.NoContent();
    }

    [Authorize]
    public static Ok<UserInfoResponse> UserInfo(ClaimsPrincipal user)
    {
        return TypedResults.Ok(new UserInfoResponse(
            user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier),
            user.FindFirstValue(ClaimTypes.Name),
            user.FindFirstValue(ClaimTypes.Email),
            [.. user.FindAll(ClaimTypes.Role).Select(c => c.Value)],
            [.. user.FindAll("permissions").Select(c => c.Value)]));
    }

    public static Ok<object> Jwks(IKeyMaterialService keys) => TypedResults.Ok(keys.GetJwksDocument());

    // The same body ValidationFilter writes, so a blank field looks the same however it is caught.
    private static ValidationProblem Invalid(IDictionary<string, string[]> errors) =>
        TypedResults.ValidationProblem(errors, detail: ValidationFilter<object>.Detail,
            title: ValidationFilter<object>.Title, type: ValidationFilter<object>.Type);

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

    // Wire names are unchanged from the inline handlers: username/password, userId/refreshToken, snake_case tokens.
    internal sealed record LoginRequest(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("password")] string Password);

    internal sealed record RefreshRequest(
        [property: JsonPropertyName("userId")] string UserId,
        [property: JsonPropertyName("refreshToken")] string RefreshToken);

    internal sealed record LogoutRequest(
        [property: JsonPropertyName("userId")] string UserId,
        [property: JsonPropertyName("refreshToken")] string RefreshToken);

    internal sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_at_utc")] DateTimeOffset ExpiresAtUtc,
        [property: JsonPropertyName("refresh_token")] string RefreshToken)
    {
        public static TokenResponse From(TokenPair pair) =>
            new(pair.AccessToken, "Bearer", pair.ExpiresAtUtc, pair.RefreshToken);
    }

    internal sealed record UserInfoResponse(
        [property: JsonPropertyName("sub")] string? Sub,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("roles")] string[] Roles,
        [property: JsonPropertyName("permissions")] string[] Permissions);
}
