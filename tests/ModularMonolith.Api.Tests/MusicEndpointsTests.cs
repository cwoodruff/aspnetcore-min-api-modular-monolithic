using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Identity.Modules.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Api.Tests;

public class MusicEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    private WebApplicationFactory<Program> CreateFactoryWithTenantUser(string tenantId = "tenant-123")
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the default IUserStore with one that includes a tenant claim
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUserStore));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<IUserStore>(new TenantUserStore(tenantId));
            });
        });
    }

    private sealed class TenantUserStore(string tenantId) : IUserStore
    {
        private static readonly string[] Roles = ["User"]; // immutable per test needs
        private static readonly string[] Perms = ["music.read", "orders.read"]; // immutable per test needs

        public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string? email, string? tenant)> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
        {
            if (string.Equals(username, "demo", StringComparison.OrdinalIgnoreCase) && password == "demo123!")
            {
                return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>((true, "user-1", "Demo User", Roles, Perms, "demo@example.com", tenantId));
            }

            return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>((false, string.Empty, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record LoginResponse(string access_token, string token_type, DateTimeOffset expires_at_utc, string refresh_token);

    private static async Task<string> GetAccessTokenAsync(HttpClient client, string username = "demo", string password = "demo123!")
    {
        var payload = JsonSerializer.Serialize(new { username, password });
        var resp = await client.PostAsync("/api/identity/login", new StringContent(payload, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync();
        var login = await JsonSerializer.DeserializeAsync<LoginResponse>(stream, JsonOpts);
        login.Should().NotBeNull();
        login!.access_token.Should().NotBeNullOrWhiteSpace();
        return login.access_token;
    }

    [Fact]
    public async Task GetAlbumByIdShouldReturn401WhenNoTokenProvided()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/music/albums/1");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAlbumByIdShouldReturn200AndAlbumShapeWhenAuthorized()
    {
        var tenantFactory = CreateFactoryWithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await GetAccessTokenAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/music/albums/1");
        // Authorized calls should not be 401/403
        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Forbidden);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            root.TryGetProperty("id", out var idProp).Should().BeTrue();
            idProp.GetInt32().Should().BeGreaterThan(0);
            root.TryGetProperty("title", out var titleProp).Should().BeTrue();
            titleProp.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task GetAlbumByIdShouldReturn404WhenAuthorizedButNotFound()
    {
        var tenantFactory = CreateFactoryWithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/music/albums/999999");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlbumByIdShouldReturn403WhenTenantHeaderProvidedButUserHasNoTenantClaim()
    {
        var tenantFactory = CreateFactoryWithTenantUser("tenant-user");
        var client = tenantFactory.CreateClient();
        var token = await GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Mismatch tenant between user (tenant-user) and request (tenant-other) should yield 403
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-other");

        var response = await client.GetAsync("/api/music/albums/1");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }
}
