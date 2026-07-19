using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

public class TrackEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task GetTrackById_ShouldReturn401_WhenNoToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/music/tracks/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTrackById_ShouldReturn200Shape_WhenAuthorized()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.GetAsync("/api/music/tracks/1");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            root.TryGetProperty("Id", out var idProp).Should().BeTrue();
            idProp.GetInt32().Should().BeGreaterThan(0);
            root.TryGetProperty("Name", out var nameProp).Should().BeTrue();
            nameProp.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData("/api/music/tracks/")]
    [InlineData("/api/music/tracks/artist/1")]
    [InlineData("/api/music/tracks/playlist/1")]
    [InlineData("/api/music/tracks/album/1")]
    [InlineData("/api/music/tracks/genre/1")]
    [InlineData("/api/music/tracks/mediatype/1")]
    [InlineData("/api/music/tracks/invoice/1")]
    public async Task Track_Collections_ShouldReturn200_WhenAuthorized(string url)
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);
        var response = await client.GetAsync(url);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTrackById_ShouldReturn403_WhenTenantMismatch()
    {
        var tenantFactory = _factory.WithTenantUser("tenant-user");
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token, "tenant-other");
        var response = await client.GetAsync("/api/music/tracks/1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
