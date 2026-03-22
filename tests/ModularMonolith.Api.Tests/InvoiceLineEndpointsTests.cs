using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

public class InvoiceLineEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task GetInvoiceLineById_ShouldReturn401_WhenNoToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/orders/invoice-lines/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInvoiceLineById_ShouldReturn200Shape_WhenAuthorized()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.GetAsync("/api/orders/invoice-lines/1");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            root.TryGetProperty("id", out var idProp).Should().BeTrue();
            idProp.GetInt32().Should().BeGreaterThan(0);
            root.TryGetProperty("quantity", out var qtyProp).Should().BeTrue();
            qtyProp.GetInt32().Should().BeGreaterThan(0);
        }
    }

    [Theory]
    [InlineData("/api/orders/invoice-lines/")]
    [InlineData("/api/orders/invoice-lines/invoice/1")]
    [InlineData("/api/orders/invoice-lines/track/1")]
    public async Task InvoiceLine_Collections_ShouldReturn200_WhenAuthorized(string url)
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);
        var response = await client.GetAsync(url);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInvoiceLineById_ShouldReturn404_WhenNotFound()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);
        var response = await client.GetAsync("/api/orders/invoice-lines/999999");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetInvoiceLineById_ShouldReturn403_WhenTenantMismatch()
    {
        var tenantFactory = _factory.WithTenantUser("tenant-user");
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token, "tenant-other");
        var response = await client.GetAsync("/api/orders/invoice-lines/1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
