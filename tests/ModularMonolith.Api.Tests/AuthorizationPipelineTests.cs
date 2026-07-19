using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

public class AuthorizationPipelineTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithConfiguredIdentityUsersInDevelopment();

    [Fact]
    public async Task AdminLogin_ShouldIssueAdminClaims_AndAllowCustomerList()
    {
        var client = _factory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(
            client,
            TestAuthHelpers.AdminUser.Username,
            TestAuthHelpers.AdminUser.Password);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Where(claim => claim.Type == ClaimTypes.Role).Select(claim => claim.Value)
            .Should().Contain("Admin");
        jwt.Claims.Where(claim => claim.Type == "permissions").Select(claim => claim.Value)
            .Should().Contain(["administration.read", "administration.write", "admin.users.manage"]);
        jwt.Claims.Single(claim => claim.Type == "tenant").Value.Should().Be(TestAuthHelpers.AdminUser.Tenant);

        client.UseBearer(token, TestAuthHelpers.AdminUser.Tenant);
        var response = await client.GetAsync("/api/admin/customers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NormalUserLogin_ShouldAllowMusicRead()
    {
        var client = _factory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(
            client,
            TestAuthHelpers.DemoUser.Username,
            TestAuthHelpers.DemoUser.Password);

        client.UseBearer(token, TestAuthHelpers.DemoUser.Tenant);
        var response = await client.GetAsync("/api/music/albums/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NormalUserLogin_ShouldBeForbidden_FromAdminCustomers()
    {
        var client = _factory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(
            client,
            TestAuthHelpers.DemoUser.Username,
            TestAuthHelpers.DemoUser.Password);

        client.UseBearer(token, TestAuthHelpers.DemoUser.Tenant);
        var response = await client.GetAsync("/api/admin/customers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
