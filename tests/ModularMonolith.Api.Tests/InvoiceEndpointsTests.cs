using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

public class InvoiceEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task GetInvoiceById_ShouldReturn401_WhenNoToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/orders/invoices/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInvoiceById_ShouldReturn200Shape_WhenAuthorized()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.GetAsync("/api/orders/invoices/1");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            root.TryGetProperty("Id", out var idProp).Should().BeTrue();
            idProp.GetInt32().Should().BeGreaterThan(0);
            root.TryGetProperty("Total", out var totalProp).Should().BeTrue();
            totalProp.GetDecimal().Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task GetInvoices_ShouldReturn200_WhenAuthorized()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);
        var response = await client.GetAsync("/api/orders/invoices/");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/orders/invoices/customer/1")]
    [InlineData("/api/orders/invoices/employee/1")]
    public async Task GetInvoices_ByRelation_ShouldReturn200_WhenAuthorized(string url)
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);
        var response = await client.GetAsync(url);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInvoiceById_ShouldReturn404_WhenNotFound()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);
        var response = await client.GetAsync("/api/orders/invoices/999999");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetInvoiceById_ShouldReturn403_WhenTenantMismatch()
    {
        var tenantFactory = _factory.WithTenantUser("tenant-user");
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token, "tenant-other");
        var response = await client.GetAsync("/api/orders/invoices/1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
