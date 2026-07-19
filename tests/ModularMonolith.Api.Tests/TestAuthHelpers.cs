using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Identity.Modules.Authorization;
using Identity.Modules.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Api.Tests;

public static class TestAuthHelpers
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly SeededIdentityUser DemoUser = SeededIdentityUser.Create(
        "demo",
        "user-1",
        "Demo User",
        ["User"],
        [Permissions.MusicRead],
        "demo@example.com",
        "tenant-1");

    public static readonly SeededIdentityUser UserMoUser = SeededIdentityUser.Create(
        "usermo",
        "user-2",
        "Music+Orders User",
        ["User"],
        [Permissions.MusicRead, Permissions.OrdersRead],
        "usermo@example.com",
        "tenant-1");

    public static readonly SeededIdentityUser ReportUser = SeededIdentityUser.Create(
        "report",
        "user-3",
        "Reporting User",
        ["User"],
        [Permissions.ReportView],
        "report@example.com",
        "tenant-1");

    public static readonly SeededIdentityUser AdminUser = SeededIdentityUser.Create(
        "admin",
        "admin-1",
        "Administrator",
        ["Admin"],
        [
            Permissions.MusicRead,
            Permissions.MusicWrite,
            Permissions.OrdersRead,
            Permissions.OrdersWrite,
            Permissions.AdminUsersManage,
            Permissions.AdministrationRead,
            Permissions.AdministrationWrite,
            Permissions.ReportView
        ],
        "admin@example.com",
        "tenant-1");

    public static WebApplicationFactory<Program> WithSeededIdentityUsers(this WebApplicationFactory<Program> factory)
    {
        var users = new[] { DemoUser, UserMoUser, ReportUser, AdminUser };

        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(BuildPersistenceConfiguration());
                config.AddInMemoryCollection(BuildIdentityUserConfiguration(users));
            });
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUserStore));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<IUserStore>(new SeededUserStore(users));
            });
        });
    }

    public static WebApplicationFactory<Program> WithConfiguredIdentityUsers(this WebApplicationFactory<Program> factory)
    {
        var users = new[] { DemoUser, UserMoUser, ReportUser, AdminUser };

        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(BuildPersistenceConfiguration());
                config.AddInMemoryCollection(BuildIdentityUserConfiguration(users));
            });
        });
    }

    public static WebApplicationFactory<Program> WithConfiguredIdentityUsersInDevelopment(
        this WebApplicationFactory<Program> factory)
    {
        var users = new[] { DemoUser, UserMoUser, ReportUser, AdminUser };

        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(BuildPersistenceConfiguration());
                config.AddInMemoryCollection(BuildIdentityUserConfiguration(users));
            });
        });
    }

    public static WebApplicationFactory<Program> WithTenantUser(this WebApplicationFactory<Program> factory,
        string tenantId = "tenant-123", string[]? permissions = null, string[]? roles = null)
    {
        permissions ??= new[] { "music.read", "orders.read" };
        roles ??= new[] { "User" };

        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(BuildPersistenceConfiguration());
            });
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUserStore));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<IUserStore>(new TenantUserStore(tenantId, roles, permissions));
            });
        });
    }

    public static WebApplicationFactory<Program> WithAdminTenantUser(this WebApplicationFactory<Program> factory,
        string tenantId = "tenant-123", string[]? permissions = null, string[]? roles = null)
    {
        permissions ??=
        [
            Permissions.MusicRead,
            Permissions.OrdersRead,
            Permissions.AdminUsersManage,
            Permissions.AdministrationRead,
            Permissions.AdministrationWrite
        ];
        roles ??= ["Admin"];

        return factory.WithTenantUser(tenantId, permissions, roles);
    }

    public static async Task<string> GetAccessTokenAsync(HttpClient client, string? username = null,
        string? password = null)
    {
        username ??= DemoUser.Username;
        password ??= DemoUser.Password;

        var payload = JsonSerializer.Serialize(new { username, password });
        var resp = await client.PostAsync("/api/identity/login",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync();
        var login = await JsonSerializer.DeserializeAsync<LoginResponse>(stream, JsonOpts);
        if (login is null || string.IsNullOrWhiteSpace(login.access_token))
        {
            throw new InvalidOperationException("Failed to acquire access token for test user.");
        }

        return login.access_token;
    }

    public static void UseBearer(this HttpClient client, string token, string tenantId = "tenant-123")
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
    }

    public sealed record LoginResponse(
        string access_token,
        string token_type,
        DateTimeOffset expires_at_utc,
        string refresh_token);

    public sealed record SeededIdentityUser(
        string Username,
        string Password,
        string UserId,
        string DisplayName,
        string[] Roles,
        string[] Permissions,
        string Email,
        string Tenant)
    {
        public static SeededIdentityUser Create(string username, string userId, string displayName, string[] roles,
            string[] permissions, string email, string tenant)
        {
            return new SeededIdentityUser(
                username,
                $"T-{Guid.NewGuid():N}!Aa1",
                userId,
                displayName,
                roles,
                permissions,
                email,
                tenant);
        }
    }

    private sealed class TenantUserStore(string tenantId, string[] roles, string[] permissions) : IUserStore
    {
        public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string?
            email, string? tenant)> ValidateCredentialsAsync(string username, string password,
            CancellationToken ct = default)
        {
            if (string.Equals(username, DemoUser.Username, StringComparison.OrdinalIgnoreCase)
                && password == DemoUser.Password)
            {
                return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>((true, "user-1",
                    "Demo User", roles, permissions, "demo@example.com", tenantId));
            }

            return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>((false, string.Empty,
                null, Array.Empty<string>(), Array.Empty<string>(), null, null));
        }

        public Task<(bool found, string? displayName, string[] roles, string[] permissions, string? email, string? tenant)>
            GetUserByIdAsync(string userId, CancellationToken ct = default)
        {
            if (string.Equals(userId, "user-1", StringComparison.Ordinal))
            {
                return Task.FromResult<(bool, string?, string[], string[], string?, string?)>(
                    (true, "Demo User", roles, permissions, "demo@example.com", tenantId));
            }

            return Task.FromResult<(bool, string?, string[], string[], string?, string?)>(
                (false, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
        }
    }

    private sealed class SeededUserStore(SeededIdentityUser[] users) : IUserStore
    {
        private readonly Dictionary<string, SeededIdentityUser> _usersByUsername =
            users.ToDictionary(user => user.Username, StringComparer.OrdinalIgnoreCase);

        public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string?
            email, string? tenant)> ValidateCredentialsAsync(string username, string password,
            CancellationToken ct = default)
        {
            if (_usersByUsername.TryGetValue(username, out var user) && password == user.Password)
            {
                return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>(
                    (true, user.UserId, user.DisplayName, user.Roles, user.Permissions, user.Email, user.Tenant));
            }

            return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>(
                (false, string.Empty, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
        }

        public Task<(bool found, string? displayName, string[] roles, string[] permissions, string? email, string? tenant)>
            GetUserByIdAsync(string userId, CancellationToken ct = default)
        {
            var user = users.FirstOrDefault(candidate => string.Equals(candidate.UserId, userId, StringComparison.Ordinal));
            if (user is not null)
            {
                return Task.FromResult<(bool, string?, string[], string[], string?, string?)>(
                    (true, user.DisplayName, user.Roles, user.Permissions, user.Email, user.Tenant));
            }

            return Task.FromResult<(bool, string?, string[], string[], string?, string?)>(
                (false, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
        }
    }

    private static Dictionary<string, string?> BuildIdentityUserConfiguration(SeededIdentityUser[] users)
    {
        var configuration = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < users.Length; i++)
        {
            var user = users[i];
            configuration[$"Identity:InMemoryUsers:{i}:Username"] = user.Username;
            configuration[$"Identity:InMemoryUsers:{i}:Password"] = user.Password;
            configuration[$"Identity:InMemoryUsers:{i}:UserId"] = user.UserId;
            configuration[$"Identity:InMemoryUsers:{i}:DisplayName"] = user.DisplayName;
            configuration[$"Identity:InMemoryUsers:{i}:Email"] = user.Email;
            configuration[$"Identity:InMemoryUsers:{i}:Tenant"] = user.Tenant;

            for (var roleIndex = 0; roleIndex < user.Roles.Length; roleIndex++)
            {
                configuration[$"Identity:InMemoryUsers:{i}:Roles:{roleIndex}"] = user.Roles[roleIndex];
            }

            for (var permissionIndex = 0; permissionIndex < user.Permissions.Length; permissionIndex++)
            {
                configuration[$"Identity:InMemoryUsers:{i}:Permissions:{permissionIndex}"] =
                    user.Permissions[permissionIndex];
            }
        }

        return configuration;
    }

    private static Dictionary<string, string?> BuildPersistenceConfiguration()
    {
        var root = FindRepositoryRoot();
        var sourceDbPath = Path.Combine(root, "src", "ModularMonolith.Api", "data", "chinook.db");
        var testDataDirectory = Path.Combine(AppContext.BaseDirectory, "TestData");
        Directory.CreateDirectory(testDataDirectory);

        var dbPath = Path.Combine(testDataDirectory, $"chinook-{Guid.NewGuid():N}.db");
        using (var source = new FileStream(sourceDbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var destination = new FileStream(dbPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            source.CopyTo(destination);
        }

        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:AppDatabase"] = $"Data Source={dbPath}"
        };
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ModularMonolith.Api.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root for test database configuration.");
    }
}
