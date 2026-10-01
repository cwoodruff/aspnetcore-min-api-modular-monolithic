using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;

namespace ModularMonolith.Api.Tests.Repositories;

[Collection(RepositoryDatabaseDefinition.Name)]
public class CustomerRepositoryTests(RepositoryDatabaseFixture database)
{
    [Fact]
    public async Task GetBySupportRepId_ShouldReturnCustomers()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new CustomerRepository(ctx);

        var customers = await repo.GetBySupportRepId(2);
        customers.Should().HaveCount(1);
        customers[0].SupportRepId.Should().Be(2);
    }

    [Fact]
    public async Task GetBySupportRepId_ShouldReturnEmptyForUnknownRep()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new CustomerRepository(ctx);

        var customers = await repo.GetBySupportRepId(999);
        customers.Should().BeEmpty();
    }
}
