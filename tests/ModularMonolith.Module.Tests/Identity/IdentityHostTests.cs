using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Identity;

/// <summary>
///     Identity on its own host, with its real authentication rather than the test scheme. It owns no tables,
///     so the host has no database. The rest of Identity's tests stay in Api.Tests, which replace its
///     services inside the full host.
/// </summary>
public sealed class IdentityHostTests : IAsyncLifetime
{
    private const string Password = "T-identity-host!Aa1";
    private ModuleTestHost<global::Identity.Modules.IdentityModule.Modules> _host = null!;

    public async Task InitializeAsync() =>
        _host = await ModuleTestHost.StartAsync<global::Identity.Modules.IdentityModule.Modules>(ModuleData.Identity, seeded: false,
            configuration: new Dictionary<string, string?>
            {
                ["Identity:InMemoryUsers:0:Username"] = "reader",
                ["Identity:InMemoryUsers:0:Password"] = Password,
                ["Identity:InMemoryUsers:0:UserId"] = "user-9",
                ["Identity:InMemoryUsers:0:DisplayName"] = "Reader",
                ["Identity:InMemoryUsers:0:Email"] = "reader@example.com",
                ["Identity:InMemoryUsers:0:Tenant"] = "tenant-9",
                ["Identity:InMemoryUsers:0:Roles:0"] = "User",
                ["Identity:InMemoryUsers:0:Permissions:0"] = "catalog.read"
            });

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task AConfiguredUser_LogsIn_AndTheTokenIsAcceptedByUserInfo()
    {
        var client = _host.CreateClient();

        var login = await (await client.PostAsync("/api/identity/login", Json.Body(new { username = "reader", password = Password })))
            .ReadAsync(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("access_token").GetString());

        var user = await (await client.GetAsync("/api/identity/userinfo")).ReadAsync(HttpStatusCode.OK);
        user.GetProperty("sub").GetString().Should().Be("user-9");
        user.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().Equal("catalog.read");
    }

    [Fact]
    public async Task AWrongPassword_Returns401() =>
        (await _host.CreateClient().PostAsync("/api/identity/login", Json.Body(new { username = "reader", password = "wrong" })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task TheSigningKeys_ArePublished()
    {
        var jwks = await (await _host.CreateClient().GetAsync("/api/identity/.well-known/jwks.json")).ReadAsync(HttpStatusCode.OK);

        jwks.GetProperty("keys").GetArrayLength().Should().BeGreaterThan(0);
    }
}
