using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;

namespace ModularMonolith.Api.Tests.Repositories;

public class EmployeeRepositoryTests
{
    [Fact]
    public async Task GetReportsTo_ShouldReturnEmployeeById()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new EmployeeRepository(ctx);

        var emp = await repo.GetReportsTo(1);
        emp.Should().NotBeNull();
        emp.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetDirectReports_ShouldReturnEmployeesReportingToManager()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new EmployeeRepository(ctx);

        var reports = await repo.GetDirectReports(1);
        reports.Should().HaveCount(1);
        reports[0].ReportsTo.Should().Be(1);
        reports[0].FirstName.Should().Be("John");
    }
}