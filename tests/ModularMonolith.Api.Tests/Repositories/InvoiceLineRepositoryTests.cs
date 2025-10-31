using System;
using System.Threading.Tasks;
using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;

namespace ModularMonolith.Api.Tests.Repositories;

public class InvoiceLineRepositoryTests
{
    [Fact]
    public async Task GetByInvoiceId_ShouldReturnLines()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new InvoiceLineRepository(ctx);

        var lines = await repo.GetByInvoiceId(1);
        lines.Should().HaveCount(2);
        lines.Should().OnlyContain(l => l.InvoiceId == 1);
    }

    [Fact]
    public async Task GetByTrackId_ShouldReturnLines()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new InvoiceLineRepository(ctx);

        var lines = await repo.GetByTrackId(1);
        lines.Should().HaveCount(1);
        lines[0].TrackId.Should().Be(1);
    }

    [Fact]
    public async Task UnknownIds_ShouldReturnEmpty()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new InvoiceLineRepository(ctx);

        (await repo.GetByInvoiceId(999)).Should().BeEmpty();
        (await repo.GetByTrackId(999)).Should().BeEmpty();
    }
}