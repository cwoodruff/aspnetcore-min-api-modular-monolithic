using System;
using System.Threading.Tasks;
using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;

namespace ModularMonolith.Api.Tests.Repositories;

public class InvoiceRepositoryTests
{
    [Fact]
    public async Task GetByCustomerId_ShouldReturnInvoicesForCustomer()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new InvoiceRepository(ctx);

        var invoices = await repo.GetByCustomerId(1);
        invoices.Should().HaveCount(1);
        invoices[0].CustomerId.Should().Be(1);
    }

    [Fact]
    public async Task UnknownIds_ShouldReturnEmpty()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new InvoiceRepository(ctx);

        (await repo.GetByCustomerId(999)).Should().BeEmpty();
    }
}
