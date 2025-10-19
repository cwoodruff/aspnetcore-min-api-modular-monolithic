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
    // Static demo users for module-scoped authorization scenarios.
    // All users share the same tenant for simplicity in demos.
    private const string DefaultTenant = "tenant-1";

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

    private static readonly string[] RolesUser = ["User"]; // reused immutable array
    private static readonly string[] RolesAdmin = ["Admin"]; // reused immutable array

    private static readonly string[] PermsMusicRead = [Identity.Modules.Authorization.Permissions.MusicRead];
    private static readonly string[] PermsMusicOrdersRead = [Identity.Modules.Authorization.Permissions.MusicRead, Identity.Modules.Authorization.Permissions.OrdersRead];
    private static readonly string[] PermsReportOnly = [Identity.Modules.Authorization.Permissions.ReportView];
    private static readonly string[] PermsAdminAll = [
        Identity.Modules.Authorization.Permissions.MusicRead,
        Identity.Modules.Authorization.Permissions.MusicWrite,
        Identity.Modules.Authorization.Permissions.OrdersRead,
        Identity.Modules.Authorization.Permissions.OrdersWrite,
        Identity.Modules.Authorization.Permissions.AdminUsersManage,
        Identity.Modules.Authorization.Permissions.ReportView
    ];

    private static readonly Dictionary<string, UserRecord> Users = new(StringComparer.OrdinalIgnoreCase)
    {
        // 1) Demo user: Music only
        ["demo"] = new UserRecord(
            Username: "demo",
            Password: "demo123!",
            UserId: "user-1",
            Display: "Demo User",
            Roles: RolesUser,
            Perms: PermsMusicRead,
            Email: "demo@example.com",
            Tenant: DefaultTenant
        ),

        // 2) New user: Music + Orders
        ["usermo"] = new UserRecord(
            Username: "usermo",
            Password: "usermo123!",
            UserId: "user-2",
            Display: "Music+Orders User",
            Roles: RolesUser,
            Perms: PermsMusicOrdersRead,
            Email: "usermo@example.com",
            Tenant: DefaultTenant
        ),

        // 3) Reporting-only user
        ["report"] = new UserRecord(
            Username: "report",
            Password: "report123!",
            UserId: "user-3",
            Display: "Reporting User",
            Roles: RolesUser,
            Perms: PermsReportOnly,
            Email: "report@example.com",
            Tenant: DefaultTenant
        ),

        // 4) Admin user: can access all modules
        ["admin"] = new UserRecord(
            Username: "admin",
            Password: "admin123!",
            UserId: "admin-1",
            Display: "Administrator",
            Roles: RolesAdmin,
            Perms: PermsAdminAll,
            Email: "admin@example.com",
            Tenant: DefaultTenant
        ),
    };

    public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string? email, string? tenant)> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        if (Users.TryGetValue(username, out var user) && password == user.Password)
        {
            return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>(
                (true, user.UserId, user.Display, user.Roles, user.Perms, user.Email, user.Tenant));
        }

        return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>(
            (false, string.Empty, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
    }
}
