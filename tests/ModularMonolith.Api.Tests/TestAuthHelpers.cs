using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Identity.Modules.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Api.Tests;

public static class TestAuthHelpers
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static WebApplicationFactory<Program> WithTenantUser(this WebApplicationFactory<Program> factory,
        string tenantId = "tenant-123", string[]? permissions = null, string[]? roles = null)
    {
        permissions ??= new[] { "music.read", "orders.read", "administration.read" };
        roles ??= new[] { "User" };

        return factory.WithWebHostBuilder(builder =>
        {
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

    public static async Task<string> GetAccessTokenAsync(HttpClient client, string username = "demo",
        string password = "demo123!")
    {
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

    public static void UseBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public sealed record LoginResponse(
        string access_token,
        string token_type,
        DateTimeOffset expires_at_utc,
        string refresh_token);

    private sealed class TenantUserStore(string tenantId, string[] roles, string[] permissions) : IUserStore
    {
        public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string?
            email, string? tenant)> ValidateCredentialsAsync(string username, string password,
            CancellationToken ct = default)
        {
            if (string.Equals(username, "demo", StringComparison.OrdinalIgnoreCase) && password == "demo123!")
            {
                return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>((true, "user-1",
                    "Demo User", roles, permissions, "demo@example.com", tenantId));
            }

            return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>((false, string.Empty,
                null, Array.Empty<string>(), Array.Empty<string>(), null, null));
        }
    }
}
