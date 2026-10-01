using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;

namespace ModularMonolith.Api.Tests.Repositories;

[Collection(RepositoryDatabaseDefinition.Name)]
public class InvoiceLineRepositoryTests(RepositoryDatabaseFixture database)
{
    [Fact]
    public async Task GetByInvoiceId_ShouldReturnLines()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new InvoiceLineRepository(ctx);

        var lines = await repo.GetByInvoiceId(1);
        lines.Should().HaveCount(2);
        lines.Should().OnlyContain(l => l.InvoiceId == 1);
    }

    [Fact]
    public async Task GetByTrackId_ShouldReturnLines()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new InvoiceLineRepository(ctx);

        var lines = await repo.GetByTrackId(1);
        lines.Should().HaveCount(1);
        lines[0].TrackId.Should().Be(1);
    }

    [Fact]
    public async Task UnknownIds_ShouldReturnEmpty()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new InvoiceLineRepository(ctx);

        (await repo.GetByInvoiceId(999)).Should().BeEmpty();
        (await repo.GetByTrackId(999)).Should().BeEmpty();
    }
}
