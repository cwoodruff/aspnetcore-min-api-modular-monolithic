using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ModularMonolith.Module.Tests.Hosting;
using Orders.Contracts.Events;

namespace ModularMonolith.Module.Tests.Administration;

/// <summary>Administration's InvoiceFinalized handler, in isolation: delivered through the module's own inbox (ADR-0010).</summary>
public sealed class InvoiceFinalizedHandlerTests(AdministrationFixture database) : IClassFixture<AdministrationFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAndSeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnInvoiceFinalized_AddsToTheCustomersSummary()
    {
        (await database.Host.DeliverAsync(Event())).Should().Be(1);

        await using var administration = database.CreateAdministrationContext();
        var summary = await administration.CustomerPurchaseSummaries.SingleAsync(s => s.CustomerId == TestData.Customer);
        summary.InvoiceCount.Should().Be(1);
        summary.TotalSpent.Should().Be(2.28m);
    }

    [Fact]
    public async Task TheSameEventDeliveredTwice_TakesEffectOnce()
    {
        var finalized = Event();

        (await database.Host.DeliverAsync(finalized)).Should().Be(1);
        (await database.Host.DeliverAsync(finalized)).Should().Be(0, "the module's inbox already has this event");

        await using var administration = database.CreateAdministrationContext();
        (await administration.CustomerPurchaseSummaries.SingleAsync()).InvoiceCount.Should().Be(1);
    }

    private static InvoiceFinalized Event() => new(Guid.NewGuid(), DateTimeOffset.UtcNow, TestData.Invoice, TestData.Customer,
        new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 2.28m,
        [new InvoiceFinalizedLine(TestData.Track1, 1, 0.99m), new InvoiceFinalizedLine(TestData.Track2, 1, 1.29m)]);
}
