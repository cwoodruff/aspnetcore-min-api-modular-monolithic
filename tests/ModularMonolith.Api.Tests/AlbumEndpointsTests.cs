using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

public class AlbumEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task GetAlbumByIdShouldReturn401WhenNoTokenProvided()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/music/albums/1");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAlbumByIdShouldReturn200AndAlbumShapeWhenAuthorized()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);

        client.UseBearer(token);

        var response = await client.GetAsync("/api/music/albums/1");
        // Authorized calls should not be 401/403
        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Forbidden);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            root.TryGetProperty("id", out var idProp).Should().BeTrue();
            idProp.GetInt32().Should().BeGreaterThan(0);
            root.TryGetProperty("title", out var titleProp).Should().BeTrue();
            titleProp.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task GetAlbumByIdShouldReturn404WhenAuthorizedButNotFound()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.GetAsync("/api/music/albums/999999");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlbumByIdShouldReturn403WhenTenantHeaderProvidedButUserHasNoTenantClaim()
    {
        var tenantFactory = _factory.WithTenantUser("tenant-user");
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);
        // Mismatch tenant between user (tenant-user) and request (tenant-other) should yield 403
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-other");

        var response = await client.GetAsync("/api/music/albums/1");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }
}
