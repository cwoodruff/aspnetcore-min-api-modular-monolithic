using FluentAssertions;
using Identity.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Events;
using System.Net;
using System.Text.Json;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     The one flow that needs three modules: Orders finalizes an invoice, and only after the outbox is
///     dispatched do Catalog's track sales and Administration's purchase summary reflect it. Each module's
///     side is tested in its own host in ModularMonolith.Module.Tests.
/// </summary>
public class FinalizedInvoiceFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] Everything =
    [
        Permissions.CatalogRead, Permissions.OrdersRead, Permissions.OrdersWrite,
        Permissions.AdministrationRead, Permissions.AdministrationWrite
    ];

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
