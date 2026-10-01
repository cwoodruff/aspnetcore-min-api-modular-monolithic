using System.Net;
using FluentAssertions;
using Identity.Contracts;
using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Administration;

/// <summary>Administration's HTTP surface in its own host, on its share of the Chinook seed.</summary>
public sealed class AdministrationEndpointsTests(SeededAdministrationFixture administration)
    : IClassFixture<SeededAdministrationFixture>
{
    private HttpClient Admin => administration.Host.CreateClient(TestUser.Admin());

    private HttpClient ReadOnlyAdmin =>
        administration.Host.CreateClient(TestUser.Admin(permissions: [Permissions.AdministrationRead]));

    [Theory]
    [InlineData("/api/admin/customers/1")]
    [InlineData("/api/admin/employees/1")]
    [InlineData("/api/admin/genres/1")]
    [InlineData("/api/admin/media-types/1")]
    public async Task ById_Returns401_WithoutAUser(string url) =>
        (await administration.Host.CreateClient().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Theory]
    [InlineData("/api/admin/customers/1")]
    [InlineData("/api/admin/employees/1/reports-to")]
    public async Task Returns403_WhenTheRequestTenantIsNotTheUsers(string url)
    {
        var client = administration.Host.CreateClient(TestUser.Admin("tenant-user"), requestTenant: "tenant-other");

        (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CustomerPurchases_Return403_ForNonAdmin()
    {
        var client = administration.Host.CreateClient(TestUser.TenantUser(
            permissions: [Permissions.AdministrationRead, Permissions.AdministrationWrite]));

        (await client.GetAsync("/api/admin/customers/1/purchases")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/admin/customers/1", "FirstName", "Luís")]
    [InlineData("/api/admin/employees/1", "FirstName", "Andrew")]
    [InlineData("/api/admin/genres/1", "Name", "Rock")]
    [InlineData("/api/admin/media-types/1", "Name", "MPEG audio file")]
    public async Task ById_ReturnsTheSeededRow(string url, string property, string expected)
    {
        var row = await (await Admin.GetAsync(url)).ReadAsync(HttpStatusCode.OK);

        row.GetProperty("Id").GetInt32().Should().Be(1);
        row.GetProperty(property).GetString().Should().Be(expected);
    }

    [Theory]
    [InlineData("/api/admin/customers/999999")]
    [InlineData("/api/admin/genres/999999")]
    public async Task ById_Returns404_WhenMissing(string url) =>
        (await Admin.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);

    [Theory]
    [InlineData("/api/admin/customers/support-rep/3")]
    [InlineData("/api/admin/employees/1/direct-reports")]
    [InlineData("/api/admin/genres/")]
    [InlineData("/api/admin/media-types/")]
    public async Task Collections_ReturnSeededRows(string url)
    {
        var rows = await (await Admin.GetAsync(url)).ReadAsync(HttpStatusCode.OK);

        rows.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ARepeatedRead_ReturnsTheSameBody()
    {
        var first = await Admin.GetStringAsync("/api/admin/genres/");

        (await Admin.GetStringAsync("/api/admin/genres/")).Should().Be(first);
    }

    [Fact]
    public async Task GenreWrites_Return401_WithoutAUser()
    {
        var client = administration.Host.CreateClient();

        (await client.PostAsync("/api/admin/genres", Json.Body(new { name = "Jazz" }))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsync("/api/admin/genres/1", Json.Body(new { name = "Jazz" }))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync("/api/admin/genres/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GenreWrites_Return403_WithoutAdministrationWrite()
    {
        (await ReadOnlyAdmin.PostAsync("/api/admin/genres", Json.Body(new { name = "Jazz" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadOnlyAdmin.PutAsync("/api/admin/genres/1", Json.Body(new { name = "Jazz" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadOnlyAdmin.DeleteAsync("/api/admin/genres/1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateGenre_Returns201_WithLocation_AndTheGenreCanBeRead()
    {
        var name = $"Genre {Guid.NewGuid():N}"[..30];

        var response = await Admin.PostAsync("/api/admin/genres", Json.Body(new { name }));

        var created = await response.ReadAsync(HttpStatusCode.Created);
        var id = created.GetProperty("Id").GetInt32();
        response.Headers.Location!.ToString().Should().Be($"/api/admin/genres/{id}");
        (await (await Admin.GetAsync($"/api/admin/genres/{id}")).ReadAsync(HttpStatusCode.OK))
            .GetProperty("Name").GetString().Should().Be(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("121 characters")]
    public async Task CreateGenre_Returns400_ForAnInvalidName(string name)
    {
        name = name == "121 characters" ? new string('A', 121) : name;

        (await Admin.PostAsync("/api/admin/genres", Json.Body(new { name }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateGenre_RenamesIt_And404sWhenMissing()
    {
        var created = await (await Admin.PostAsync("/api/admin/genres", Json.Body(new { name = "Before" }))).ReadAsync(HttpStatusCode.Created);
        var id = created.GetProperty("Id").GetInt32();

        var updated = await (await Admin.PutAsync($"/api/admin/genres/{id}", Json.Body(new { name = "After" }))).ReadAsync(HttpStatusCode.OK);

        updated.GetProperty("name").GetString().Should().Be("After");
        (await (await Admin.GetAsync($"/api/admin/genres/{id}")).ReadAsync(HttpStatusCode.OK))
            .GetProperty("Name").GetString().Should().Be("After", "the write evicts the cached genre");
        (await Admin.PutAsync("/api/admin/genres/999999", Json.Body(new { name = "After" }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Admin.PutAsync($"/api/admin/genres/{id}", Json.Body(new { name = "" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteGenre_Returns204_ThenTheGenreIsGone_And404sWhenMissing()
    {
        var created = await (await Admin.PostAsync("/api/admin/genres", Json.Body(new { name = "Doomed" }))).ReadAsync(HttpStatusCode.Created);
        var id = created.GetProperty("Id").GetInt32();

        (await Admin.DeleteAsync($"/api/admin/genres/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Admin.GetAsync($"/api/admin/genres/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Admin.DeleteAsync("/api/admin/genres/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
