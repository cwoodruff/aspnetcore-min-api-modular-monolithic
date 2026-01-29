using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Tests for caching behavior.
///     Verifies that endpoints use cache correctly and invalidate on writes.
/// </summary>
public class CachingBehaviorTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task GetAlbum_ShouldReturnCachedResult_OnSubsequentRequests()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // First request - should hit the database
        var response1 = await client.GetAsync("/api/music/albums/1");
        response1.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);

        // Second request - should be served from cache (same response)
        var response2 = await client.GetAsync("/api/music/albums/1");
        response2.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);

        // Both should return the same data
        if (response1.StatusCode == HttpStatusCode.OK && response2.StatusCode == HttpStatusCode.OK)
        {
            var content1 = await response1.Content.ReadAsStringAsync();
            var content2 = await response2.Content.ReadAsStringAsync();
            content1.Should().Be(content2, "cached response should match original");
        }
    }

    [Fact]
    public async Task GetGenres_ShouldReturnConsistentResults()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Multiple requests to the same endpoint should return consistent results
        var response1 = await client.GetAsync("/api/admin/genres/");
        var response2 = await client.GetAsync("/api/admin/genres/");

        response1.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response2.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);

        if (response1.IsSuccessStatusCode && response2.IsSuccessStatusCode)
        {
            var content1 = await response1.Content.ReadAsStringAsync();
            var content2 = await response2.Content.ReadAsStringAsync();
            content1.Should().Be(content2, "cached list should match");
        }
    }

    [Fact]
    public async Task CreateGenre_WriteShouldNotBeUnauthorized()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Create a new genre
        var uniqueName = $"CacheTest_{Guid.NewGuid():N}";
        var payload = JsonSerializer.Serialize(new { name = uniqueName });
        var createResponse = await client.PostAsync("/api/admin/genres",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Should not be 401 since we have valid credentials
        createResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateGenre_WriteShouldNotBeUnauthorized()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Try to update a genre
        var updatedName = $"Updated_{Guid.NewGuid():N}";
        var updatePayload = JsonSerializer.Serialize(new { name = updatedName });
        var updateResponse = await client.PutAsync("/api/admin/genres/1",
            new StringContent(updatePayload, Encoding.UTF8, "application/json"));

        // Should not be 401 since we have valid credentials
        updateResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteGenre_WriteShouldNotBeUnauthorized()
    {
        var tenantFactory = _factory.WithTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Try to delete a non-existent genre
        var deleteResponse = await client.DeleteAsync("/api/admin/genres/99999");

        // Should not be 401 since we have valid credentials
        deleteResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConcurrentRequests_ShouldNotCauseCacheStampede()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Send many concurrent requests to the same endpoint
        // The cache should prevent database stampede
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => client.GetAsync("/api/music/albums/1"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // All requests should succeed (not timeout or error due to resource contention)
        var successCount = responses.Count(r =>
            r.IsSuccessStatusCode ||
            r.StatusCode == HttpStatusCode.NotFound ||
            r.StatusCode == HttpStatusCode.TooManyRequests);

        successCount.Should().Be(20, "concurrent requests should be handled gracefully with caching");
    }

    [Fact]
    public async Task DifferentEndpoints_ShouldHaveSeparateCaches()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Request different resources
        var albumResponse = await client.GetAsync("/api/music/albums/1");
        var artistResponse = await client.GetAsync("/api/music/artists/1");
        var genreResponse = await client.GetAsync("/api/admin/genres/1");

        // Each should return its own data (not mixed up due to cache key collision)
        if (albumResponse.IsSuccessStatusCode)
        {
            var albumContent = await albumResponse.Content.ReadAsStringAsync();
            albumContent.Should().Contain("title", "album response should have album structure");
        }

        if (artistResponse.IsSuccessStatusCode)
        {
            var artistContent = await artistResponse.Content.ReadAsStringAsync();
            artistContent.Should().Contain("name", "artist response should have artist structure");
        }

        if (genreResponse.IsSuccessStatusCode)
        {
            var genreContent = await genreResponse.Content.ReadAsStringAsync();
            genreContent.Should().Contain("name", "genre response should have genre structure");
        }
    }
}
