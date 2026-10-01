using System.Net;
using System.Text.Json;
using FluentAssertions;
using Identity.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

/// <summary>Reporting's HTTP surface through the full host (ADR-0014, ADR-0015).</summary>
public class ReportingEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task SalesByGenre_ReturnsEveryGenre_ForReportViewers()
    {
        var client = await ClientAsync(factory.WithTenantUser(permissions: [Permissions.ReportView]));

        using var rows = JsonDocument.Parse(await client.GetStringAsync("/api/reporting/sales-by-genre"));

        rows.RootElement.GetArrayLength().Should().Be(25, "stock Chinook has 25 genres");
        rows.RootElement.EnumerateArray().Should().AllSatisfy(row => row.GetProperty("UnitsSold").GetInt64().Should().Be(0,
            "nothing has been finalized through the outbox in a fresh database"));
    }

    [Fact]
    public async Task InvoiceLines_CarryTrackAndCustomerNames()
    {
        var client = await ClientAsync(factory.WithTenantUser(permissions: [Permissions.ReportView]));

        using var lines = JsonDocument.Parse(await client.GetStringAsync("/api/reporting/invoices/1/lines"));

        lines.RootElement.GetArrayLength().Should().BeGreaterThan(0);
        lines.RootElement.EnumerateArray().Should().AllSatisfy(line =>
        {
            line.GetProperty("TrackName").GetString().Should().NotBeNullOrEmpty();
            line.GetProperty("CustomerName").GetString().Should().NotBeNullOrEmpty();
        });
        (await client.GetAsync("/api/reporting/invoices/999999/lines")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReportEndpoints_ShouldReturn403_WithoutReportView()
    {
        var client = await ClientAsync(factory.WithTenantUser(permissions: [Permissions.CatalogRead]));

        (await client.GetAsync("/api/reporting/sales-by-genre")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IntegrityRun_FindsNothingInStockData_AndListsNoOpenFindings_ForAdmin()
    {
        var client = await ClientAsync(factory.WithAdminTenantUser());

        var run = await client.PostAsync("/api/reporting/integrity/run", null);
        run.StatusCode.Should().Be(HttpStatusCode.OK);
        using var result = JsonDocument.Parse(await run.Content.ReadAsStringAsync());
        result.RootElement.GetProperty("Detected").GetInt32().Should().Be(0);
        result.RootElement.GetProperty("Open").GetInt32().Should().Be(0);

        using var findings = JsonDocument.Parse(await client.GetStringAsync("/api/reporting/integrity/findings"));
        findings.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task IntegrityEndpoints_ShouldReturn403_ForNonAdmin()
    {
        var client = await ClientAsync(factory.WithTenantUser(permissions: [Permissions.ReportView]));

        (await client.PostAsync("/api/reporting/integrity/run", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/reporting/integrity/findings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpClient> ClientAsync(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        client.UseBearer(await TestAuthHelpers.GetAccessTokenAsync(client));
        return client;
    }
}
