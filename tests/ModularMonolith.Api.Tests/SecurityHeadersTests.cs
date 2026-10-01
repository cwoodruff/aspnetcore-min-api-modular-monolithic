using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     The OWASP security headers must be present on every response, including the ones
///     written by the exception handler (which clears the response before writing).
/// </summary>
public class SecurityHeadersTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] SecurityHeaders =
    [
        "X-Content-Type-Options",
        "X-Frame-Options",
        "X-XSS-Protection",
        "Referrer-Policy",
        "Content-Security-Policy",
        "Permissions-Policy"
    ];

    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task SuccessResponse_ShouldCarryAllSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task ValidationFailure_ShouldCarryAllSecurityHeaders()
    {
        var tenantFactory = _factory.WithAdminTenantUser(permissions: ["administration.read", "administration.write"]);
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.PostAsync("/api/admin/genres",
            new StringContent("{\"Name\":null}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        AssertSecurityHeaders(response);
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        foreach (var header in SecurityHeaders)
        {
            response.Headers.Contains(header).Should().BeTrue($"'{header}' should be present on a {(int)response.StatusCode} response");
        }
    }
}
