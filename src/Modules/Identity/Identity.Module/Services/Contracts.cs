namespace Identity.Modules.Services;

public interface IUserStore
{
    Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string? email, string?
        tenant)> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default);
}

public interface IRefreshTokenStore
{
    Task StoreAsync(string userId, string refreshToken, DateTimeOffset expires, string? clientId,
        CancellationToken ct = default);

    Task<bool> ValidateAsync(string userId, string refreshToken, CancellationToken ct = default);
    Task RevokeAsync(string userId, string refreshToken, CancellationToken ct = default);
}

public interface ITokenService
{
    Task<TokenPair> IssueAsync(string userId, string? displayName, string[] roles, string[] permissions, string? email,
        string? tenant, CancellationToken ct = default);

    Task<TokenPair?> RefreshAsync(string userId, string refreshToken, CancellationToken ct = default);
}

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);
