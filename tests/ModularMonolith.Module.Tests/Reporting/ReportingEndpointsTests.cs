using System.Net;
using FluentAssertions;
using Identity.Contracts;
using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Reporting;

/// <summary>
///     Reporting's HTTP surface in its own host (ADR-0014, ADR-0015). Its views read the other modules'
///     schemas, so this host's database has all four, seeded.
/// </summary>
public sealed class ReportingEndpointsTests(SeededReportingFixture reporting) : IClassFixture<SeededReportingFixture>
{
    private HttpClient Viewer => reporting.Host.CreateClient(TestUser.TenantUser(permissions: [Permissions.ReportView]));

    [Fact]
    public async Task SalesByGenre_ReturnsEveryGenre_ForReportViewers()
    {
        var rows = await (await Viewer.GetAsync("/api/reporting/sales-by-genre")).ReadAsync(HttpStatusCode.OK);

        rows.GetArrayLength().Should().Be(25, "stock Chinook has 25 genres");
        rows.EnumerateArray().Should().AllSatisfy(row => row.GetProperty("UnitsSold").GetInt64().Should().Be(0,
            "nothing has been finalized through the outbox in a fresh database"));
    }

    [Fact]
    public async Task InvoiceLines_CarryTrackAndCustomerNames()
    {
        var lines = await (await Viewer.GetAsync("/api/reporting/invoices/1/lines")).ReadAsync(HttpStatusCode.OK);

        lines.GetArrayLength().Should().BeGreaterThan(0);
        lines.EnumerateArray().Should().AllSatisfy(line =>
        {
            line.GetProperty("TrackName").GetString().Should().NotBeNullOrEmpty();
            line.GetProperty("CustomerName").GetString().Should().NotBeNullOrEmpty();
        });
        (await Viewer.GetAsync("/api/reporting/invoices/999999/lines")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReportEndpoints_Return403_WithoutReportView()
    {
        var client = reporting.Host.CreateClient(TestUser.TenantUser(permissions: [Permissions.CatalogRead]));

        (await client.GetAsync("/api/reporting/sales-by-genre")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IntegrityRun_FindsNothingInStockData_AndListsNoOpenFindings_ForAdmin()
    {
        var client = reporting.Host.CreateClient(TestUser.Admin());

        var result = await (await client.PostAsync("/api/reporting/integrity/run", null)).ReadAsync(HttpStatusCode.OK);

        result.GetProperty("Detected").GetInt32().Should().Be(0);
        result.GetProperty("Open").GetInt32().Should().Be(0);
        (await (await client.GetAsync("/api/reporting/integrity/findings")).ReadAsync(HttpStatusCode.OK)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task IntegrityEndpoints_Return403_ForNonAdmin()
    {
        (await Viewer.PostAsync("/api/reporting/integrity/run", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Viewer.GetAsync("/api/reporting/integrity/findings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
