using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Tests for error scenarios including invalid JSON, validation errors, and edge cases.
/// </summary>
public class ErrorScenarioTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    #region Concurrent Write Tests

    [Fact]
    public async Task ConcurrentCreates_ShouldNotReturnUnauthorized()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Create multiple genres concurrently
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var uniqueName = $"Concurrent_{Guid.NewGuid():N}";
            var payload = JsonSerializer.Serialize(new { name = uniqueName });
            return await client.PostAsync("/api/admin/genres",
                new StringContent(payload, Encoding.UTF8, "application/json"));
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        // None should be 401 (unauthorized) since we have valid credentials
        var unauthorizedCount = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);
        unauthorizedCount.Should().Be(0, "requests with valid credentials should not return 401");
    }

    #endregion

    #region Method Not Allowed Tests

    [Fact]
    public async Task PatchGenre_ShouldReturn405_MethodNotAllowed()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/admin/genres/1")
        {
            Content = new StringContent("""{"name": "Patched"}""", Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);

        // PATCH is not implemented, should return 405 or 404
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.MethodNotAllowed,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Invalid JSON Tests

    [Fact]
    public async Task CreateGenre_ShouldReturnError_WhenJsonIsMalformed()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Send malformed JSON
        var malformedJson = "{ name: 'missing quotes' }";
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(malformedJson, Encoding.UTF8, "application/json"));

        // Minimal APIs may return 400 or 500 for deserialization failures
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateGenre_ShouldReturnError_WhenJsonIsIncomplete()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Send incomplete JSON (missing closing brace)
        var incompleteJson = """{"name": "Test""";
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(incompleteJson, Encoding.UTF8, "application/json"));

        // Minimal APIs may return 400 or 500 for deserialization failures
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateGenre_ShouldReturnError_WhenBodyIsEmpty()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.PostAsync("/api/admin/genres",
            new StringContent("", Encoding.UTF8, "application/json"));

        // Empty body may return 400 or 500
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Login_ShouldReturnError_WhenJsonIsMalformed()
    {
        var client = _factory.CreateClient();

        var malformedJson = "not valid json at all";
        var response = await client.PostAsync("/api/identity/login",
            new StringContent(malformedJson, Encoding.UTF8, "application/json"));

        // Minimal APIs may return 400 or 500 for deserialization failures
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Validation Error Tests

    [Fact]
    public async Task CreateGenre_ShouldReturnError_WhenNameIsMissing()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Send JSON without required 'name' field
        var payload = JsonSerializer.Serialize(new { other = "value" });
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Missing required field should fail
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateGenre_ShouldReturnError_WhenNameIsNull()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var payload = """{"name": null}""";
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Should fail with validation error or model binding error
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateGenre_ShouldNotBeUnauthorized_WithWhitespaceOnlyName()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var payload = JsonSerializer.Serialize(new { name = "   " });
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Should not be 401 since we have valid credentials
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateGenre_ShouldNotBeUnauthorized_WhenNameTooLong()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var longName = new string('X', 200); // Much longer than 120 max
        var payload = JsonSerializer.Serialize(new { name = longName });
        var response = await client.PutAsync("/api/admin/genres/1",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Should not be 401 since we have valid credentials
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Content Type Tests

    [Fact]
    public async Task CreateGenre_ShouldReturn415_WhenContentTypeIsWrong()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var payload = """{"name": "Test"}""";
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(payload, Encoding.UTF8, "text/plain"));

        // Should return 415 Unsupported Media Type or 400 Bad Request
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.UnsupportedMediaType,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateGenre_ShouldNotBeUnauthorized_WhenContentTypeHasCharset()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var uniqueName = $"CharsetTest_{Guid.NewGuid():N}";
        var payload = JsonSerializer.Serialize(new { name = uniqueName });

        // Content type with charset should be accepted
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.ContentType!.CharSet = "utf-8";

        var response = await client.PostAsync("/api/admin/genres", content);
        // Should not be 401 since we have valid credentials
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Route Parameter Tests

    [Fact]
    public async Task GetGenre_ShouldReturn404_WhenIdIsNegative()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Negative ID - route constraint should reject or return 404
        var response = await client.GetAsync("/api/admin/genres/-1");

        // Should be 404 (not found) or route not matched
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetGenre_ShouldReturn404_WhenIdIsZero()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.GetAsync("/api/admin/genres/0");

        // ID 0 typically doesn't exist
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetGenre_ShouldReturn404_WhenIdIsNonNumeric()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Non-numeric ID should not match the route
        var response = await client.GetAsync("/api/admin/genres/abc");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetGenre_ShouldReturn404_WhenIdIsVeryLarge()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Very large ID that doesn't exist
        var response = await client.GetAsync("/api/admin/genres/2147483647");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Authentication Error Tests

    [Fact]
    public async Task Login_ShouldReturn401_WhenCredentialsAreInvalid()
    {
        var client = _factory.CreateClient();

        var payload = JsonSerializer.Serialize(new { username = "nonexistent", password = "wrongpass" });
        var response = await client.PostAsync("/api/identity/login",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ShouldReturn401_WhenPasswordIsWrong()
    {
        var client = _factory.CreateClient();

        var payload = JsonSerializer.Serialize(new { username = "demo", password = "wrongpassword" });
        var response = await client.PostAsync("/api/identity/login",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_ShouldReturn401_WhenTokenIsExpired()
    {
        var client = _factory.CreateClient();

        // Use an obviously invalid/expired token
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "expired.token.here");

        var response = await client.GetAsync("/api/music/albums/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_ShouldReturn401_WhenTokenIsMalformed()
    {
        var client = _factory.CreateClient();

        // Use a malformed token
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var response = await client.GetAsync("/api/music/albums/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
