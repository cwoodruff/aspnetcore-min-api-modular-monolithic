using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ModularMonolith.Module.Tests.Hosting;
using Orders.Contracts.Events;

namespace ModularMonolith.Module.Tests.Catalog;

/// <summary>Catalog's InvoiceFinalized handler, in isolation: delivered through the module's own inbox (ADR-0009).</summary>
public sealed class InvoiceFinalizedHandlerTests(CatalogFixture database) : IClassFixture<CatalogFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAndSeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnInvoiceFinalized_CountsEachLinesTrack()
    {
        (await database.Host.DeliverAsync(Event())).Should().Be(1);

        await using var catalog = database.CreateCatalogContext();
        var sales = await catalog.TrackSales.OrderBy(s => s.TrackId).ToListAsync();
        sales.Select(s => (s.TrackId, s.TimesSold)).Should().Equal((TestData.Track1, 1), (TestData.Track2, 1));
    }

    [Fact]
    public async Task TheSameEventDeliveredTwice_TakesEffectOnce()
    {
        var finalized = Event();

        (await database.Host.DeliverAsync(finalized)).Should().Be(1);
        (await database.Host.DeliverAsync(finalized)).Should().Be(0, "the module's inbox already has this event");

        await using var catalog = database.CreateCatalogContext();
        (await catalog.TrackSales.SumAsync(s => s.TimesSold)).Should().Be(2);
    }

    private static InvoiceFinalized Event() => new(Guid.NewGuid(), DateTimeOffset.UtcNow, TestData.Invoice, TestData.Customer,
        new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 2.28m,
        [new InvoiceFinalizedLine(TestData.Track1, 1, 0.99m), new InvoiceFinalizedLine(TestData.Track2, 1, 1.29m)]);
}
