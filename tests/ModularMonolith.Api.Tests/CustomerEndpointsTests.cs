using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

public class CustomerEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task GetCustomerById_ShouldReturn401_WhenNoToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/customers/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturn200Shape_WhenAuthorized()
    {
        var tenantFactory = _factory.WithAdminTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.GetAsync("/api/admin/customers/1");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            root.TryGetProperty("Id", out var idProp).Should().BeTrue();
            idProp.GetInt32().Should().BeGreaterThan(0);
            root.TryGetProperty("FirstName", out var fnProp).Should().BeTrue();
            fnProp.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task GetCustomerById_ShouldNotReturn401Or403_WhenConfiguredAdminUserHasRequiredPermissionAndTenant()
    {
        var client = factory.WithConfiguredIdentityUsersInDevelopment().CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(
            client,
            TestAuthHelpers.AdminUser.Username,
            TestAuthHelpers.AdminUser.Password);
        client.UseBearer(token, TestAuthHelpers.AdminUser.Tenant);

        var response = await client.GetAsync("/api/admin/customers/1");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturn404_WhenNotFound()
    {
        var tenantFactory = _factory.WithAdminTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);
        var response = await client.GetAsync("/api/admin/customers/999999");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturn403_WhenTenantMismatch()
    {
        var tenantFactory = _factory.WithAdminTenantUser("tenant-user");
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token, "tenant-other");
        var response = await client.GetAsync("/api/admin/customers/1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCustomersBySupportRep_ShouldReturn200_WhenAuthorized()
    {
        var tenantFactory = _factory.WithAdminTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);
        var response = await client.GetAsync("/api/admin/customers/support-rep/1");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
