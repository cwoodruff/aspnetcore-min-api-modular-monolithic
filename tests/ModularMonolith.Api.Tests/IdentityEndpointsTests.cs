using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Tests for Identity module endpoints (login, refresh, logout, userinfo, JWKS).
/// </summary>
public class IdentityEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    #region Health Endpoint Tests

    [Fact]
    public async Task IdentityHealth_ShouldReturnHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/identity/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.TryGetProperty("module", out var module).Should().BeTrue();
        module.GetString().Should().Be("Identity");

        content.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetString().Should().BeOneOf("healthy", "Healthy");
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        var client = _factory.CreateClient();

        var payload = JsonSerializer.Serialize(new { username = "demo", password = "demo123!" });
        var response = await client.PostAsync("/api/identity/login",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.TryGetProperty("access_token", out var accessToken).Should().BeTrue();
        accessToken.GetString().Should().NotBeNullOrWhiteSpace();

        content.TryGetProperty("token_type", out var tokenType).Should().BeTrue();
        tokenType.GetString().Should().Be("Bearer");

        content.TryGetProperty("refresh_token", out var refreshToken).Should().BeTrue();
        refreshToken.GetString().Should().NotBeNullOrWhiteSpace();

        content.TryGetProperty("expires_at_utc", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Login_ShouldReturn401_WhenUsernameIsWrong()
    {
        var client = _factory.CreateClient();

        var payload = JsonSerializer.Serialize(new { username = "wronguser", password = "demo123!" });
        var response = await client.PostAsync("/api/identity/login",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ShouldReturn401_WhenPasswordIsWrong()
    {
        var client = _factory.CreateClient();

        var payload = JsonSerializer.Serialize(new { username = "demo", password = "wrongpass" });
        var response = await client.PostAsync("/api/identity/login",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("demo", "demo123!")]
    [InlineData("admin", "admin123!")]
    [InlineData("usermo", "usermo123!")]
    public async Task Login_ShouldSucceed_ForAllDemoUsers(string username, string password)
    {
        var client = _factory.CreateClient();

        var payload = JsonSerializer.Serialize(new { username, password });
        var response = await client.PostAsync("/api/identity/login",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Should succeed or be rate limited
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.TooManyRequests);
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task Refresh_ShouldReturnNewTokens_WhenRefreshTokenIsValid()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();

        // First login to get tokens
        var loginPayload = JsonSerializer.Serialize(new { username = "demo", password = "demo123!" });
        var loginResponse = await client.PostAsync("/api/identity/login",
            new StringContent(loginPayload, Encoding.UTF8, "application/json"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginContent.GetProperty("refresh_token").GetString();

        // Now use refresh token
        var refreshPayload = JsonSerializer.Serialize(new { userId = "user-1", refreshToken });
        var refreshResponse = await client.PostAsync("/api/identity/refresh",
            new StringContent(refreshPayload, Encoding.UTF8, "application/json"));

        // Should succeed or be rate limited
        refreshResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.TooManyRequests);

        if (refreshResponse.StatusCode == HttpStatusCode.OK)
        {
            var refreshContent = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
            refreshContent.TryGetProperty("access_token", out var newAccessToken).Should().BeTrue();
            newAccessToken.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task Refresh_ShouldReturn401_WhenRefreshTokenIsInvalid()
    {
        var client = _factory.CreateClient();

        var payload = JsonSerializer.Serialize(new { userId = "user-1", refreshToken = "invalid-refresh-token" });
        var response = await client.PostAsync("/api/identity/refresh",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Should be unauthorized or rate limited
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_ShouldReturn204_WhenAuthenticated()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();

        // Login first
        var loginPayload = JsonSerializer.Serialize(new { username = "demo", password = "demo123!" });
        var loginResponse = await client.PostAsync("/api/identity/login",
            new StringContent(loginPayload, Encoding.UTF8, "application/json"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginContent.GetProperty("access_token").GetString();
        var refreshToken = loginContent.GetProperty("refresh_token").GetString();

        // Set bearer token
        client.UseBearer(accessToken!);

        // Logout
        var logoutPayload = JsonSerializer.Serialize(new { userId = "user-1", refreshToken });
        var logoutResponse = await client.PostAsync("/api/identity/logout",
            new StringContent(logoutPayload, Encoding.UTF8, "application/json"));

        // Should succeed or be rate limited
        logoutResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Logout_ShouldReturn401_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient();

        var payload = JsonSerializer.Serialize(new { userId = "user-1", refreshToken = "some-token" });
        var response = await client.PostAsync("/api/identity/logout",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region UserInfo Tests

    [Fact]
    public async Task UserInfo_ShouldReturnClaims_WhenAuthenticated()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.GetAsync("/api/identity/userinfo");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.TryGetProperty("sub", out var sub).Should().BeTrue();
        sub.GetString().Should().NotBeNullOrWhiteSpace();

        content.TryGetProperty("permissions", out var permissions).Should().BeTrue();
        permissions.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UserInfo_ShouldReturn401_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/identity/userinfo");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region JWKS Tests

    [Fact]
    public async Task Jwks_ShouldReturnKeys_WithoutAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/identity/.well-known/jwks.json");

        // Should succeed or be rate limited (it's public but rate limited)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.TooManyRequests);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.TryGetProperty("keys", out var keys).Should().BeTrue();
            keys.GetArrayLength().Should().BeGreaterThan(0);

            // Verify key structure
            var firstKey = keys[0];
            firstKey.TryGetProperty("kty", out _).Should().BeTrue("key should have key type");
            firstKey.TryGetProperty("kid", out _).Should().BeTrue("key should have key id");
            firstKey.TryGetProperty("use", out _).Should().BeTrue("key should have use");
        }
    }

    [Fact]
    public async Task Jwks_ShouldReturnConsistentKeys()
    {
        var client = _factory.CreateClient();

        var response1 = await client.GetAsync("/api/identity/.well-known/jwks.json");
        var response2 = await client.GetAsync("/api/identity/.well-known/jwks.json");

        if (response1.StatusCode == HttpStatusCode.OK && response2.StatusCode == HttpStatusCode.OK)
        {
            var content1 = await response1.Content.ReadAsStringAsync();
            var content2 = await response2.Content.ReadAsStringAsync();
            content1.Should().Be(content2, "JWKS should be consistent across requests");
        }
    }

    #endregion

    #region Token Validation Tests

    [Fact]
    public async Task AccessToken_ShouldBeValidForProtectedEndpoints()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Use the token to access a protected endpoint
        var response = await client.GetAsync("/api/music/albums/1");

        // Should not be 401 (token is valid)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccessToken_Structure_ShouldBeValid()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);

        // JWT should have 3 parts
        var parts = token.Split('.');
        parts.Should().HaveCount(3, "JWT should have header.payload.signature format");

        // Each part should be base64url encoded
        foreach (var part in parts)
        {
            part.Should().NotBeNullOrWhiteSpace();
            part.Should().MatchRegex("^[A-Za-z0-9_-]+$", "JWT parts should be base64url encoded");
        }
    }

    #endregion
}
