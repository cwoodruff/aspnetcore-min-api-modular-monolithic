using System.Collections.Concurrent;
using Identity.Modules.Authorization;
using Microsoft.Extensions.Options;

namespace Identity.Modules.Services;

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, (string token, DateTimeOffset expires)> _store = new();

    public Task StoreAsync(string userId, string refreshToken, DateTimeOffset expires, string? clientId,
        CancellationToken ct = default)
    {
        _store[Key(userId, refreshToken)] = (refreshToken, expires);
        return Task.CompletedTask;
    }

    public Task<bool> ValidateAsync(string userId, string refreshToken, CancellationToken ct = default)
    {
        if (_store.TryGetValue(Key(userId, refreshToken), out var entry))
        {
            if (entry.expires > DateTimeOffset.UtcNow)
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public Task RevokeAsync(string userId, string refreshToken, CancellationToken ct = default)
    {
        _store.TryRemove(Key(userId, refreshToken), out _);
        return Task.CompletedTask;
    }

    private static string Key(string userId, string token)
    {
        return $"{userId}:{token}";
    }
}

public sealed class InMemoryUserStore : IUserStore
{
    private readonly IReadOnlyDictionary<string, UserRecord> _users;

    public InMemoryUserStore(IOptions<InMemoryUserStoreOptions> options)
    {
        _users = options.Value.Users
            .Where(user => !string.IsNullOrWhiteSpace(user.Username)
                           && !string.IsNullOrWhiteSpace(user.Password)
                           && !string.IsNullOrWhiteSpace(user.UserId))
            .Select(user => new UserRecord(
                user.Username.Trim(),
                user.Password,
                user.UserId.Trim(),
                string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username.Trim() : user.DisplayName.Trim(),
                user.Roles.Where(role => !string.IsNullOrWhiteSpace(role)).ToArray(),
                user.Permissions.Where(permission => !string.IsNullOrWhiteSpace(permission)).ToArray(),
                string.IsNullOrWhiteSpace(user.Email) ? null : user.Email.Trim(),
                string.IsNullOrWhiteSpace(user.Tenant) ? null : user.Tenant.Trim()))
            .ToDictionary(user => user.Username, StringComparer.OrdinalIgnoreCase);
    }

    public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string? email,
        string? tenant)> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        if (_users.TryGetValue(username, out var user) && password == user.Password)
        {
            return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>(
                (true, user.UserId, user.Display, user.Roles, user.Perms, user.Email, user.Tenant));
        }

        return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>(
            (false, string.Empty, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
    }

    public Task<(bool found, string? displayName, string[] roles, string[] permissions, string? email, string? tenant)>
        GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => string.Equals(u.UserId, userId, StringComparison.Ordinal));
        if (user is not null)
        {
            return Task.FromResult<(bool, string?, string[], string[], string?, string?)>(
                (true, user.Display, user.Roles, user.Perms, user.Email, user.Tenant));
        }

        return Task.FromResult<(bool, string?, string[], string[], string?, string?)>(
            (false, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
    }

    private sealed record UserRecord(
        string Username,
        string Password,
        string UserId,
        string Display,
        string[] Roles,
        string[] Perms,
        string? Email,
        string? Tenant
    );
}

public sealed class DisabledUserStore : IUserStore
{
    public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string? email,
        string? tenant)> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>(
            (false, string.Empty, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
    }

    public Task<(bool found, string? displayName, string[] roles, string[] permissions, string? email, string? tenant)>
        GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        return Task.FromResult<(bool, string?, string[], string[], string?, string?)>(
            (false, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
    }
}

public sealed class InMemoryUserStoreOptions
{
    public List<InMemoryUserRecord> Users { get; set; } = [];
}

public sealed class InMemoryUserRecord
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
    public string[] Permissions { get; set; } = [];
    public string? Email { get; set; }
    public string? Tenant { get; set; }
}
