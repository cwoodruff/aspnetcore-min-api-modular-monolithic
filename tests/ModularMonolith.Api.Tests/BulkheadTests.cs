using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Api.Tests;

/// <summary>Bulkheads between modules through the real host (ADR-0013); one module's metrics are in Module.Tests.</summary>
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
    public void EveryModulesWorkQueue_IsAStartedHostedService()
    {
        // AddHostedService de-duplicates by type; a second module's queue was once silently dropped.
        var host = factory.WithWebHostBuilder(_ => { });

        var queues = host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<SharedKernel.Concurrency.ModuleWorkQueue>()
            .Select(queue => queue.Module)
            .Order(StringComparer.Ordinal);

        queues.Should().Equal("Orders", "Reporting");
    }
}
