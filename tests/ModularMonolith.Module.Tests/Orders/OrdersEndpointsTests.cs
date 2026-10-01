using System.Net;
using FluentAssertions;
using Identity.Contracts;
using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Orders;

/// <summary>Orders' HTTP surface in its own host, on its share of the Chinook seed.</summary>
public sealed class OrdersEndpointsTests(SeededOrdersFixture orders) : IClassFixture<SeededOrdersFixture>
{
    private HttpClient Reader => orders.Host.CreateClient(TestUser.TenantUser());

    private HttpClient Writer =>
        orders.Host.CreateClient(TestUser.TenantUser(permissions: [Permissions.OrdersRead, Permissions.OrdersWrite]));

    [Theory]
    [InlineData("/api/orders/invoices/1")]
    [InlineData("/api/orders/invoice-lines/1")]
    public async Task ById_Returns401_WithoutAUser(string url) =>
        (await orders.Host.CreateClient().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Theory]
    [InlineData("/api/orders/invoices/1")]
    [InlineData("/api/orders/invoice-lines/1")]
    public async Task ById_Returns403_WhenTheRequestTenantIsNotTheUsers(string url)
    {
        var client = orders.Host.CreateClient(TestUser.TenantUser("tenant-user"), requestTenant: "tenant-other");

        (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InvoiceById_ReturnsTheSeededInvoice()
    {
        var invoice = await (await Reader.GetAsync("/api/orders/invoices/1")).ReadAsync(HttpStatusCode.OK);

        invoice.GetProperty("Id").GetInt32().Should().Be(1);
        invoice.GetProperty("Total").GetDecimal().Should().Be(3.96m);
    }

    [Fact]
    public async Task InvoiceLineById_ReturnsTheSeededLine()
    {
        var line = await (await Reader.GetAsync("/api/orders/invoice-lines/1")).ReadAsync(HttpStatusCode.OK);

        line.GetProperty("Id").GetInt32().Should().Be(1);
        line.GetProperty("Quantity").GetInt32().Should().Be(1);
    }

    [Theory]
    [InlineData("/api/orders/invoices/999999")]
    [InlineData("/api/orders/invoice-lines/999999")]
    public async Task ById_Returns404_WhenMissing(string url) =>
        (await Reader.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);

    [Theory]
    [InlineData("/api/orders/invoices/")]
    [InlineData("/api/orders/invoices/customer/1")]
    [InlineData("/api/orders/invoice-lines/")]
    [InlineData("/api/orders/invoice-lines/invoice/1")]
    [InlineData("/api/orders/invoice-lines/track/3027")]
    public async Task Collections_ReturnSeededRows(string url)
    {
        var rows = await (await Reader.GetAsync(url)).ReadAsync(HttpStatusCode.OK);

        rows.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Finalize_Returns202_WithLocationAndAnEventualNote_AndPublishes()
    {
        var publishedBefore = orders.Host.Published.Count;

        var response = await Writer.PostAsync("/api/orders/invoices/5/finalize", null);

        var body = await response.ReadAsync(HttpStatusCode.Accepted);
        response.Headers.Location!.ToString().Should().Be("/api/orders/invoices/5");
        body.GetProperty("salesCountersUpdate").GetString().Should().Be("eventual");
        var invoice = await (await Reader.GetAsync("/api/orders/invoices/5")).ReadAsync(HttpStatusCode.OK);
        invoice.GetProperty("Status").GetString().Should().Be("Finalized");
        orders.Host.Published.Should().HaveCount(publishedBefore + 1);
    }

    [Fact]
    public async Task Finalize_Twice_Returns409()
    {
        (await Writer.PostAsync("/api/orders/invoices/6/finalize", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await Writer.PostAsync("/api/orders/invoices/6/finalize", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Finalize_Returns404_WhenTheInvoiceIsMissing() =>
        (await Writer.PostAsync("/api/orders/invoices/999999/finalize", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

    [Fact]
    public async Task Finalize_Returns403_WithoutOrdersWrite() =>
        (await Reader.PostAsync("/api/orders/invoices/5/finalize", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

    [Fact]
    public async Task DeadLetters_Return403_ForNonAdmin()
    {
        (await Writer.GetAsync("/api/orders/outbox/dead-letters")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Writer.PostAsync($"/api/orders/outbox/dead-letters/{Guid.NewGuid()}/retry", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeadLetters_ListNone_AndRejectAnUnknownRetry_ForAdmin()
    {
        var client = orders.Host.CreateClient(TestUser.Admin());

        (await (await client.GetAsync("/api/orders/outbox/dead-letters")).ReadAsync(HttpStatusCode.OK)).GetArrayLength().Should().Be(0);
        (await client.PostAsync($"/api/orders/outbox/dead-letters/{Guid.NewGuid()}/retry", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
