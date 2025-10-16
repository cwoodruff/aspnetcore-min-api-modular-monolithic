using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

public class HealthEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Theory]
    [InlineData("/", "root")]
    [InlineData("/api/music/health", "Music")]
    [InlineData("/api/orders/health", "Orders")]
    [InlineData("/api/administration/health", "Administration")]
    [InlineData("/api/reporting/health", "Reporting")]
    [InlineData("/api/identity/health", "Identity")]
    public async Task EndpointsShouldReturnHealthyAndModuleName(string url, string expectedModule)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;

        root.TryGetProperty("module", out var moduleProp).Should().BeTrue();
        root.TryGetProperty("status", out var statusProp).Should().BeTrue();

        moduleProp.GetString().Should().Be(expectedModule);
        statusProp.GetString().Should().Be("Healthy");
    }
}
