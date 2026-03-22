using System.Collections.Concurrent;
using Identity.Modules.Authorization;

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
    // Static demo users for module-scoped authorization scenarios.
    // All users share the same tenant for simplicity in demos.
    private const string DefaultTenant = "tenant-1";

    private static readonly string[] RolesUser = ["User"]; // reused immutable array
    private static readonly string[] RolesAdmin = ["Admin"]; // reused immutable array

    private static readonly string[] PermsMusicRead = [Permissions.MusicRead];
    private static readonly string[] PermsMusicOrdersRead = [Permissions.MusicRead, Permissions.OrdersRead];
    private static readonly string[] PermsReportOnly = [Permissions.ReportView];

    private static readonly string[] PermsAdminAll =
    [
        Permissions.MusicRead,
        Permissions.MusicWrite,
        Permissions.OrdersRead,
        Permissions.OrdersWrite,
        Permissions.AdminUsersManage,
        Permissions.AdministrationRead,
        Permissions.AdministrationWrite,
        Permissions.ReportView
    ];

    private static readonly Dictionary<string, UserRecord> Users = new(StringComparer.OrdinalIgnoreCase)
    {
        // 1) Demo user: Music only
        ["demo"] = new UserRecord(
            "demo",
            "demo123!",
            "user-1",
            "Demo User",
            RolesUser,
            PermsMusicRead,
            "demo@example.com",
            DefaultTenant
        ),

        // 2) New user: Music + Orders
        ["usermo"] = new UserRecord(
            "usermo",
            "usermo123!",
            "user-2",
            "Music+Orders User",
            RolesUser,
            PermsMusicOrdersRead,
            "usermo@example.com",
            DefaultTenant
        ),

        // 3) Reporting-only user
        ["report"] = new UserRecord(
            "report",
            "report123!",
            "user-3",
            "Reporting User",
            RolesUser,
            PermsReportOnly,
            "report@example.com",
            DefaultTenant
        ),

        // 4) Admin user: can access all modules
        ["admin"] = new UserRecord(
            "admin",
            "admin123!",
            "admin-1",
            "Administrator",
            RolesAdmin,
            PermsAdminAll,
            "admin@example.com",
            DefaultTenant
        )
    };

    public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string? email,
        string? tenant)> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        if (Users.TryGetValue(username, out var user) && password == user.Password)
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
        var user = Users.Values.FirstOrDefault(u => string.Equals(u.UserId, userId, StringComparison.Ordinal));
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
        string Email,
        string Tenant
    );
}
