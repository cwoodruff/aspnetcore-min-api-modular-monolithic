using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;

namespace ModularMonolith.Api.Tests.Repositories;

public class CustomerRepositoryTests
{
    [Fact]
    public async Task GetBySupportRepId_ShouldReturnCustomers()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new CustomerRepository(ctx);

        var customers = await repo.GetBySupportRepId(2);
        customers.Should().HaveCount(1);
        customers[0].SupportRepId.Should().Be(2);
    }

    [Fact]
    public async Task GetBySupportRepId_ShouldReturnEmptyForUnknownRep()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new CustomerRepository(ctx);

        var customers = await repo.GetBySupportRepId(999);
        customers.Should().BeEmpty();
    }
}
