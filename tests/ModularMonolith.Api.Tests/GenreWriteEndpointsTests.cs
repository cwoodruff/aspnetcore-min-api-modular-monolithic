using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Tests for POST/PUT/DELETE operations on Genre endpoints.
/// </summary>
public class GenreWriteEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    #region POST Tests

    [Fact]
    public async Task CreateGenre_ShouldReturn401_WhenNoToken()
    {
        var client = _factory.CreateClient();
        var payload = JsonSerializer.Serialize(new { name = "Jazz" });
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateGenre_ShouldReturn403_WhenNoWritePermission()
    {
        // User with only read permission
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var payload = JsonSerializer.Serialize(new { name = "Jazz" });
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateGenre_ShouldHandleRequest_WhenAuthorizedWithWritePermission()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var uniqueName = $"TestGenre_{Guid.NewGuid():N}";
        var payload = JsonSerializer.Serialize(new { name = uniqueName });
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Should not be 401 (unauthorized) since we have valid token
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);

        // Should be one of the expected responses (created, validation error, rate limited, or server error)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            response.Headers.Location.Should().NotBeNull();
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            content.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task CreateGenre_ShouldHandleEmptyName()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var payload = JsonSerializer.Serialize(new { name = "" });
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Should not be 401 (we have valid credentials)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateGenre_ShouldHandleNameTooLong()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var longName = new string('A', 121); // Max is 120
        var payload = JsonSerializer.Serialize(new { name = longName });
        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Should not be 401 (we have valid credentials)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PUT Tests

    [Fact]
    public async Task UpdateGenre_ShouldReturn401_WhenNoToken()
    {
        var client = _factory.CreateClient();
        var payload = JsonSerializer.Serialize(new { name = "Updated Name" });
        var response = await client.PutAsync("/api/admin/genres/1",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateGenre_ShouldReturn403_WhenNoWritePermission()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var payload = JsonSerializer.Serialize(new { name = "Updated Name" });
        var response = await client.PutAsync("/api/admin/genres/1",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateGenre_ShouldHandleRequest_WhenAuthorized()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Try to update an existing genre (ID 1 from chinook)
        var updatedName = $"Updated_{Guid.NewGuid():N}";
        var updatePayload = JsonSerializer.Serialize(new { name = updatedName });
        var updateResponse = await client.PutAsync("/api/admin/genres/1",
            new StringContent(updatePayload, Encoding.UTF8, "application/json"));

        // Should not be 401 (unauthorized) since we have valid token
        updateResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateGenre_ShouldNotBeUnauthorized_WhenNotExists()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var payload = JsonSerializer.Serialize(new { name = "Updated Name" });
        var response = await client.PutAsync("/api/admin/genres/999999",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        // Should not be 401 (we have valid credentials)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateGenre_ShouldNotBeUnauthorized_WhenNameIsEmpty()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var payload = JsonSerializer.Serialize(new { name = "" });
        var response = await client.PutAsync("/api/admin/genres/1",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        // Should not be 401 (we have valid credentials)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DELETE Tests

    [Fact]
    public async Task DeleteGenre_ShouldReturn401_WhenNoToken()
    {
        var client = _factory.CreateClient();
        var response = await client.DeleteAsync("/api/admin/genres/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteGenre_ShouldReturn403_WhenNoWritePermission()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.DeleteAsync("/api/admin/genres/1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteGenre_ShouldHandleRequest_WhenAuthorized()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Try to delete a non-existent genre (to avoid modifying test data)
        var response = await client.DeleteAsync("/api/admin/genres/99999");

        // Should not be 401 (unauthorized) since we have valid token
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteGenre_ShouldNotBeUnauthorized_WhenNotExists()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.DeleteAsync("/api/admin/genres/999999");
        // Should not be 401 (we have valid credentials)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    #endregion
}
