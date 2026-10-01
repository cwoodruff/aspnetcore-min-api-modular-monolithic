using System.Net;
using System.Text.Json;
using FluentAssertions;
using Identity.Modules.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Events;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     The HTTP surface of the InvoiceFinalized flow. Each factory gets its own database clone, so these
///     tests can finalize invoices freely. The outbox semantics themselves are covered in
///     ModularMonolith.Services.Tests (OutboxTests).
/// </summary>
public class OutboxEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] Everything =
    [
        Permissions.CatalogRead, Permissions.OrdersRead, Permissions.OrdersWrite,
        Permissions.AdministrationRead, Permissions.AdministrationWrite
    ];

    [Fact]
    public async Task Finalize_ShouldReturn202_WithLocationAndAnEventualNote()
    {
        var client = await ClientAsync(factory.WithTenantUser(permissions: Everything));

        var response = await client.PostAsync("/api/orders/invoices/5/finalize", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Location!.ToString().Should().Be("/api/orders/invoices/5");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("salesCountersUpdate").GetString().Should().Be("eventual");

        using var invoice = JsonDocument.Parse(await client.GetStringAsync("/api/orders/invoices/5"));
        invoice.RootElement.GetProperty("Status").GetString().Should().Be("Finalized");
    }

    [Fact]
    public async Task Finalize_Twice_ShouldReturn409()
    {
        var client = await ClientAsync(factory.WithTenantUser(permissions: Everything));

        (await client.PostAsync("/api/orders/invoices/6/finalize", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await client.PostAsync("/api/orders/invoices/6/finalize", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Finalize_ShouldReturn404_WhenInvoiceMissing()
    {
        var client = await ClientAsync(factory.WithTenantUser(permissions: Everything));

        (await client.PostAsync("/api/orders/invoices/999999/finalize", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Finalize_ShouldReturn403_WithoutOrdersWrite()
    {
        var client = await ClientAsync(factory.WithTenantUser(permissions: [Permissions.OrdersRead]));

        (await client.PostAsync("/api/orders/invoices/5/finalize", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FinalizedInvoice_ReachesTrackSalesAndPurchaseSummary_OnlyAfterDispatch()
    {
        // The hosted loop is off; the test triggers the dispatcher itself, so nothing races it.
        var host = factory
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:Enabled"] = "false" })))
            .WithTenantUser(permissions: Everything, roles: ["Admin"]);
        var client = await ClientAsync(host);

        using var invoice = JsonDocument.Parse(await client.GetStringAsync("/api/orders/invoices/1"));
        var customerId = invoice.RootElement.GetProperty("CustomerId").GetInt32();
        var trackId = invoice.RootElement.GetProperty("InvoiceLines")[0].GetProperty("TrackId").GetInt32();

        (await client.PostAsync("/api/orders/invoices/1/finalize", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Eventual: nothing is counted until the outbox is delivered.
        (await TimesSoldAsync(client, trackId)).Should().Be(0);

        var dispatcher = host.Services.GetServices<IHostedService>().OfType<OutboxDispatcher>().Single();
        (await dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(1);

        (await TimesSoldAsync(client, trackId)).Should().Be(1);
        using var purchases = JsonDocument.Parse(await client.GetStringAsync($"/api/admin/customers/{customerId}/purchases"));
        purchases.RootElement.GetProperty("InvoiceCount").GetInt32().Should().Be(1);
        purchases.RootElement.GetProperty("TotalSpent").GetDecimal()
            .Should().Be(invoice.RootElement.GetProperty("Total").GetDecimal());
    }

    [Fact]
    public async Task TrackSales_ShouldReturn404_WhenTrackMissing()
    {
        var client = await ClientAsync(factory.WithTenantUser());

        (await client.GetAsync("/api/catalog/tracks/999999/sales")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CustomerPurchases_ShouldReturn403_ForNonAdmin()
    {
        var client = await ClientAsync(factory.WithTenantUser(permissions: Everything));

        (await client.GetAsync("/api/admin/customers/1/purchases")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeadLetters_ShouldReturn403_ForNonAdmin()
    {
        var client = await ClientAsync(factory.WithTenantUser(permissions: Everything));

        (await client.GetAsync("/api/orders/outbox/dead-letters")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/api/orders/outbox/dead-letters/{Guid.NewGuid()}/retry", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeadLetters_ShouldListNoneAndRejectUnknownRetry_ForAdmin()
    {
        var client = await ClientAsync(factory.WithAdminTenantUser());

        using var list = JsonDocument.Parse(await client.GetStringAsync("/api/orders/outbox/dead-letters"));
        list.RootElement.GetArrayLength().Should().Be(0);
        (await client.PostAsync($"/api/orders/outbox/dead-letters/{Guid.NewGuid()}/retry", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<HttpClient> ClientAsync(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        client.UseBearer(await TestAuthHelpers.GetAccessTokenAsync(client));
        return client;
    }

    private static async Task<int> TimesSoldAsync(HttpClient client, int trackId)
    {
        using var sales = JsonDocument.Parse(await client.GetStringAsync($"/api/catalog/tracks/{trackId}/sales"));
        return sales.RootElement.GetProperty("TimesSold").GetInt32();
    }
}
