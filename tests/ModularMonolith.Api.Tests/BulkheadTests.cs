using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using SharedKernel.Diagnostics;
using System.Diagnostics.Metrics;

namespace ModularMonolith.Api.Tests;

/// <summary>Per-module rate limits and metrics through the real host (ADR-0013).</summary>
public class BulkheadTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task ExceedingCatalogsRateLimit_Returns429OnCatalog_And200OnOrders_InTheSameWindow()
    {
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Policies:catalog:api:PermitLimit"] = "3",
                ["RateLimiting:Policies:catalog:api:WindowSeconds"] = "600"
            })));
        var client = host.CreateClient();

        var catalog = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            catalog.Add((await client.GetAsync("/api/catalog/health")).StatusCode);
        }

        catalog.Should().Equal(
            HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK,
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests);
        (await client.GetAsync("/api/orders/health")).StatusCode.Should().Be(HttpStatusCode.OK,
            "Orders has its own budget; Catalog's burst does not spend it");
    }

    [Fact]
    public async Task ACatalogRequest_IncrementsTheRequestCounterTaggedWithCatalog()
    {
        var host = factory.WithWebHostBuilder(_ => { });
        var client = host.CreateClient();
        using var requests = new MetricCollector<long>(
            host.Services.GetRequiredService<IMeterFactory>(), ModuleMeter.MeterNamePrefix + "Catalog", ModuleMeter.Requests);

        (await client.GetAsync("/api/catalog/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        // The counter is recorded when the response completes, just after the client has it.
        await requests.WaitForMeasurementsAsync(1, TimeSpan.FromSeconds(5));
        var measurement = requests.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        measurement.Value.Should().Be(1);
        measurement.Tags["module"].Should().Be("Catalog");
        measurement.Tags["status_code"].Should().Be(200);
    }
}
