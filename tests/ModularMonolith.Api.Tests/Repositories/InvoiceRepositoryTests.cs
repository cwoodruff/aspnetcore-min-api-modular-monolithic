using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;

namespace ModularMonolith.Api.Tests.Repositories;

[Collection(RepositoryDatabaseDefinition.Name)]
public class InvoiceRepositoryTests(RepositoryDatabaseFixture database)
{
    [Fact]
    public async Task GetByCustomerId_ShouldReturnInvoicesForCustomer()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new InvoiceRepository(ctx);

        var invoices = await repo.GetByCustomerId(1);
        invoices.Should().HaveCount(1);
        invoices[0].CustomerId.Should().Be(1);
    }

    [Fact]
    public async Task UnknownIds_ShouldReturnEmpty()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new InvoiceRepository(ctx);

        (await repo.GetByCustomerId(999)).Should().BeEmpty();
    }
}
