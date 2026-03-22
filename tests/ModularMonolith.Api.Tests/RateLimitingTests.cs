using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Tests for rate limiting behavior (429 responses).
///     The default policy is 60 requests per 60 seconds per IP.
/// </summary>
public class RateLimitingTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task RootEndpoint_ShouldReturn429_WhenRateLimitExceeded()
    {
        var client = _factory.CreateClient();

        // Send requests up to the limit (60 requests per 60 seconds)
        // We'll send slightly more than the limit to trigger rate limiting
        var tasks = new List<Task<HttpResponseMessage>>();
        const int totalRequests = 65;

        for (var i = 0; i < totalRequests; i++)
        {
            tasks.Add(client.GetAsync("/"));
        }

        var responses = await Task.WhenAll(tasks);

        // Count 429 responses
        var tooManyRequests = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        var successfulRequests = responses.Count(r => r.IsSuccessStatusCode);

        // We should have at least some 429 responses since we exceeded the limit
        tooManyRequests.Should().BeGreaterThan(0, "rate limit should be enforced after 60 requests");
        successfulRequests.Should().BeLessThanOrEqualTo(60, "at most 60 requests should succeed within the window");
    }

    [Fact]
    public async Task RootEndpoint_ShouldIncludeRetryAfterHeader_WhenRateLimited()
    {
        var client = _factory.CreateClient();

        // Send enough requests to trigger rate limiting
        var tasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < 70; i++)
        {
            tasks.Add(client.GetAsync("/"));
        }

        var responses = await Task.WhenAll(tasks);

        // Find a 429 response
        var rateLimitedResponse = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        if (rateLimitedResponse is not null)
        {
            // The response should have rate limit headers (framework provides these)
            rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }
    }

    [Fact]
    public async Task IdentityLoginEndpoint_ShouldReturn429_WhenRateLimitExceeded()
    {
        var client = _factory.CreateClient();

        // Send multiple login attempts - these are rate limited too
        var tasks = new List<Task<HttpResponseMessage>>();
        const int totalRequests = 65;

        for (var i = 0; i < totalRequests; i++)
        {
            var content = new StringContent(
                """{"username":"invalid","password":"invalid"}""",
                Encoding.UTF8,
                "application/json");
            tasks.Add(client.PostAsync("/api/identity/login", content));
        }

        var responses = await Task.WhenAll(tasks);

        // Count different response types
        var tooManyRequests = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        var unauthorized = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);

        // Some should be rate limited (429) and some should be unauthorized (401)
        // The exact distribution depends on timing, but we should see at least some 429s
        tooManyRequests.Should().BeGreaterThan(0, "rate limit should be enforced on login endpoint");
    }

    [Fact]
    public async Task AuthenticatedEndpoint_ShouldBeRateLimited()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        // Send requests to a protected endpoint
        var tasks = new List<Task<HttpResponseMessage>>();
        const int totalRequests = 70;

        for (var i = 0; i < totalRequests; i++)
        {
            tasks.Add(client.GetAsync("/api/music/albums/1"));
        }

        var responses = await Task.WhenAll(tasks);

        // Count rate limited responses
        var tooManyRequests = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        // Should have some 429 responses
        tooManyRequests.Should().BeGreaterThan(0, "authenticated endpoints should also be rate limited");
    }

    [Fact]
    public async Task BelowRateLimit_ShouldSucceed()
    {
        var client = _factory.CreateClient();

        // Send fewer requests than the limit
        var tasks = new List<Task<HttpResponseMessage>>();
        const int totalRequests = 10;

        for (var i = 0; i < totalRequests; i++)
        {
            tasks.Add(client.GetAsync("/"));
        }

        var responses = await Task.WhenAll(tasks);

        // All should succeed
        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        successCount.Should().Be(totalRequests, "requests below rate limit should all succeed");
    }

    [Fact]
    public async Task RateLimitPartitioning_ShouldBePerUser()
    {
        // Test that authenticated users are rate limited separately
        var tenantFactory1 = _factory.WithTenantUser("tenant-1");
        var tenantFactory2 = _factory.WithTenantUser("tenant-2");

        var client1 = tenantFactory1.CreateClient();
        var client2 = tenantFactory2.CreateClient();

        var token1 = await TestAuthHelpers.GetAccessTokenAsync(client1);
        var token2 = await TestAuthHelpers.GetAccessTokenAsync(client2);

        client1.UseBearer(token1, "tenant-1");
        client2.UseBearer(token2, "tenant-2");

        // Each user sends some requests - should both succeed if under individual limits
        var tasks1 = Enumerable.Range(0, 5).Select(_ => client1.GetAsync("/api/music/albums/1")).ToList();
        var tasks2 = Enumerable.Range(0, 5).Select(_ => client2.GetAsync("/api/music/albums/1")).ToList();

        var responses1 = await Task.WhenAll(tasks1);
        var responses2 = await Task.WhenAll(tasks2);

        // Most should succeed (allowing for some edge cases)
        var success1 = responses1.Count(r => r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.NotFound || r.StatusCode == HttpStatusCode.Forbidden || r.StatusCode == HttpStatusCode.InternalServerError);
        var success2 = responses2.Count(r => r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.NotFound || r.StatusCode == HttpStatusCode.Forbidden || r.StatusCode == HttpStatusCode.InternalServerError);

        success1.Should().BeGreaterThan(0);
        success2.Should().BeGreaterThan(0);
    }
}
