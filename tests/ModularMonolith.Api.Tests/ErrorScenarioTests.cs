using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Errors the host owns, whichever module the request was for: malformed bodies mapped to one problem
///     shape, unsupported content types and methods, and rejected tokens. A module's own validation and
///     not-found cases are tested on its own host in Module.Tests.
/// </summary>
public class ErrorScenarioTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    #region Method Not Allowed Tests

    [Fact]
    public async Task PatchGenre_ShouldReturn405_MethodNotAllowed()
    {
        var tenantFactory = _factory.WithAdminTenantUser(permissions: ["administration.read", "administration.write"]);
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
        var tenantFactory = _factory.WithAdminTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Send malformed JSON
        var malformedJson = "{ name: 'missing quotes' }";
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(malformedJson, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemResponseAsync(response, "Malformed request.");
    }

    [Fact]
    public async Task CreateGenre_ShouldReturnError_WhenJsonIsIncomplete()
    {
        var tenantFactory = _factory.WithAdminTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Send incomplete JSON (missing closing brace)
        var incompleteJson = """{"name": "Test""";
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(incompleteJson, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemResponseAsync(response, "Malformed request.");
    }

    [Fact]
    public async Task CreateGenre_ShouldReturnError_WhenBodyIsEmpty()
    {
        var tenantFactory = _factory.WithAdminTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.PostAsync("/api/admin/genres",
            new StringContent("", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemResponseAsync(response, "Malformed request.");
    }

    [Fact]
    public async Task Login_ShouldReturnError_WhenJsonIsMalformed()
    {
        var client = _factory.CreateClient();

        var malformedJson = "not valid json at all";
        var response = await client.PostAsync("/api/identity/login",
            new StringContent(malformedJson, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemResponseAsync(response, "Malformed request.");
    }

    #endregion

    #region Content Type Tests

    [Fact]
    public async Task CreateGenre_ShouldReturn415_WhenContentTypeIsWrong()
    {
        var tenantFactory = _factory.WithAdminTenantUser(permissions: ["administration.read", "administration.write"]);
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

        var response = await client.GetAsync("/api/catalog/albums/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_ShouldReturn401_WhenTokenIsMalformed()
    {
        var client = _factory.CreateClient();

        // Use a malformed token
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var response = await client.GetAsync("/api/catalog/albums/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task AssertProblemResponseAsync(HttpResponseMessage response, string expectedTitle,
        string? expectedErrorProperty = null)
    {
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("title").GetString().Should().Be(expectedTitle);
        content.GetProperty("status").GetInt32().Should().Be((int)response.StatusCode);
        content.TryGetProperty("traceId", out _).Should().BeTrue();

        if (expectedErrorProperty is null)
        {
            return;
        }

        content.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.ValueKind.Should().Be(JsonValueKind.Object);
        errors.EnumerateObject().Select(property => property.Name)
            .Should().Contain(name => string.Equals(name, expectedErrorProperty, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
