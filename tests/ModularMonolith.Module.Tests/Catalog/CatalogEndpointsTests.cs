using System.Diagnostics.Metrics;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using ModularMonolith.Module.Tests.Hosting;
using SharedKernel.Diagnostics;

namespace ModularMonolith.Module.Tests.Catalog;

/// <summary>Catalog's HTTP surface in its own host, on its share of the Chinook seed.</summary>
public sealed class CatalogEndpointsTests(SeededCatalogFixture catalog) : IClassFixture<SeededCatalogFixture>
{
    private HttpClient Reader => catalog.Host.CreateClient(TestUser.TenantUser());

    [Theory]
    [InlineData("/api/catalog/albums/1")]
    [InlineData("/api/catalog/artists/1")]
    [InlineData("/api/catalog/playlists/1")]
    [InlineData("/api/catalog/tracks/1")]
    public async Task ById_Returns401_WithoutAUser(string url) =>
        (await catalog.Host.CreateClient().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Theory]
    [InlineData("/api/catalog/albums/1")]
    [InlineData("/api/catalog/artists/1")]
    [InlineData("/api/catalog/tracks/1")]
    public async Task ById_Returns403_WhenTheRequestTenantIsNotTheUsers(string url)
    {
        var client = catalog.Host.CreateClient(TestUser.TenantUser("tenant-user"), requestTenant: "tenant-other");

        (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/catalog/albums/1", "Title", "For Those About To Rock We Salute You")]
    [InlineData("/api/catalog/artists/1", "Name", "AC/DC")]
    [InlineData("/api/catalog/playlists/1", "Name", "Music")]
    [InlineData("/api/catalog/tracks/1", "Name", "For Those About To Rock (We Salute You)")]
    public async Task ById_ReturnsTheSeededRow(string url, string property, string expected)
    {
        var row = await (await Reader.GetAsync(url)).ReadAsync(HttpStatusCode.OK);

        row.GetProperty("Id").GetInt32().Should().Be(1);
        row.GetProperty(property).GetString().Should().Be(expected);
    }

    [Theory]
    [InlineData("/api/catalog/albums/999999")]
    [InlineData("/api/catalog/artists/999999")]
    [InlineData("/api/catalog/tracks/999999")]
    [InlineData("/api/catalog/tracks/999999/sales")]
    public async Task ById_Returns404_WhenMissing(string url) =>
        (await Reader.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);

    [Theory]
    [InlineData("/api/catalog/playlists/")]
    [InlineData("/api/catalog/tracks/")]
    [InlineData("/api/catalog/tracks/artist/1")]
    [InlineData("/api/catalog/tracks/playlist/1")]
    [InlineData("/api/catalog/tracks/album/1")]
    [InlineData("/api/catalog/tracks/genre/1")]
    [InlineData("/api/catalog/tracks/mediatype/1")]
    public async Task Collections_ReturnSeededRows(string url)
    {
        var rows = await (await Reader.GetAsync(url)).ReadAsync(HttpStatusCode.OK);

        rows.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TrackSales_StartAtZero_InAFreshDatabase()
    {
        var sales = await (await Reader.GetAsync("/api/catalog/tracks/1/sales")).ReadAsync(HttpStatusCode.OK);

        sales.GetProperty("TimesSold").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ARepeatedRead_ReturnsTheSameBody()
    {
        var first = await Reader.GetStringAsync("/api/catalog/albums/1");

        (await Reader.GetStringAsync("/api/catalog/albums/1")).Should().Be(first);
    }

    [Fact]
    public async Task ConcurrentReadsOfOneKey_AllSucceed()
    {
        var client = Reader;

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.GetAsync("/api/catalog/albums/1")));

        responses.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    [Fact]
    public async Task ARequest_IncrementsTheRequestCounterTaggedWithCatalog()
    {
        using var requests = new MetricCollector<long>(
            catalog.Host.Services.GetRequiredService<IMeterFactory>(), ModuleMeter.MeterNamePrefix + "Catalog", ModuleMeter.Requests);

        (await catalog.Host.CreateClient().GetAsync("/api/catalog/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        // The counter is recorded when the response completes, just after the client has it.
        await requests.WaitForMeasurementsAsync(1, TimeSpan.FromSeconds(5));
        var measurement = requests.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        measurement.Value.Should().Be(1);
        measurement.Tags["module"].Should().Be("Catalog");
        measurement.Tags["status_code"].Should().Be(200);
    }
}
