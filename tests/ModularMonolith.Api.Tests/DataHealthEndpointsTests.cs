using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

public class DataHealthEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Theory]
    [InlineData("/api/music/data-health", "Music")]
    [InlineData("/api/orders/data-health", "Orders")]
    [InlineData("/api/administration/data-health", "Administration")]
    [InlineData("/api/reporting/data-health", "Reporting")]
    [InlineData("/api/identity/data-health", "Identity")]
    public async Task EndpointsShouldReturnDataHealthyAndModuleName(string url, string expectedModule)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;

        root.TryGetProperty("module", out var moduleProp).Should().BeTrue();
        root.TryGetProperty("status", out var statusProp).Should().BeTrue();

        moduleProp.GetString().Should().Be(expectedModule);
        statusProp.GetString().Should().Be("Data-Healthy");
    }
}
