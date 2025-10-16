using System.Collections.Concurrent;

namespace Identity.Modules.Services;

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, (string token, DateTimeOffset expires)> _store = new();

    private static string Key(string userId, string token) => $"{userId}:{token}";

    public Task StoreAsync(string userId, string refreshToken, DateTimeOffset expires, string? clientId, CancellationToken ct = default)
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
}

public sealed class InMemoryUserStore : IUserStore
{
    // Demo user only
    private static readonly (string Username, string Password, string UserId, string Display, string[] Roles, string[] Perms, string Email) DemoUser
        = ("demo", "demo123!", "user-1", "Demo User", new[] { "User" }, new[] { "music.read", "orders.read" }, "demo@example.com");

    public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string? email, string? tenant)> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.Equals(username, DemoUser.Username, StringComparison.OrdinalIgnoreCase) && password == DemoUser.Password)
        {
            return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>((true, DemoUser.UserId, DemoUser.Display, DemoUser.Roles, DemoUser.Perms, DemoUser.Email, null));
        }
        return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>((false, string.Empty, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
    }
}