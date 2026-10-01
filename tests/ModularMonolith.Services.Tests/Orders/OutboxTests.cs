using Admin.Modules.Data;
using Catalog.Modules.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Orders.Contracts.Events;
using Orders.Modules.Data;
using Orders.Modules.Domain;
using Orders.Modules.Events;
using Orders.Modules.Services;
using SharedKernel.Events;

namespace ModularMonolith.Services.Tests.Orders;

/// <summary>
///     The InvoiceFinalized flow (ADR-0008 to ADR-0010): Orders publishes through its outbox, the dispatcher
///     delivers to Catalog and Administration, and each applies the event once through its own inbox.
///     The dispatcher is triggered directly; failures are forced with temporary database triggers, so no
///     test hook exists in production code.
/// </summary>
[Collection(ModuleDatabaseDefinition.Name)]
public sealed class OutboxTests(ModuleDatabaseFixture database) : IAsyncLifetime
{
    private readonly ManualTimeProvider _time = new();
    private ServiceProvider _host = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _host = ModuleHost.Build(database.ConnectionString, _time);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Finalize_WritesExactlyOneOutboxRow_WithTheInvoiceChange()
    {
        (await FinalizeAsync(TestData.Invoice)).Should().Be(FinalizeInvoiceResult.Finalized);

        await using var orders = database.CreateOrdersContext();
        (await orders.Invoices.SingleAsync(i => i.Id == TestData.Invoice)).Status.Should().Be(InvoiceStatus.Finalized);
        var row = await orders.Set<OutboxMessage>().SingleAsync();
        row.EventType.Should().Be(typeof(InvoiceFinalized).FullName);
        row.ProcessedAt.Should().BeNull();
        using var payload = System.Text.Json.JsonDocument.Parse(row.Payload);
        payload.RootElement.GetProperty("invoiceId").GetInt32().Should().Be(TestData.Invoice);
        payload.RootElement.GetProperty("lines").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Finalize_WhenTheOutboxWriteFails_RollsBackTheInvoiceChangeToo()
    {
        await using (var trigger = await ForceFailureAsync("orders", "OutboxMessage"))
        {
            await FluentActions.Invoking(() => FinalizeAsync(TestData.Invoice)).Should().ThrowAsync<DbUpdateException>();
        }

        await using var orders = database.CreateOrdersContext();
        (await orders.Invoices.SingleAsync(i => i.Id == TestData.Invoice)).Status.Should().Be(InvoiceStatus.Draft);
        (await orders.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Finalize_Twice_IsRejectedAndPublishesOnce()
    {
        await FinalizeAsync(TestData.Invoice);

        (await FinalizeAsync(TestData.Invoice)).Should().Be(FinalizeInvoiceResult.AlreadyFinalized);
        await using var orders = database.CreateOrdersContext();
        (await orders.Set<OutboxMessage>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Dispatcher_DeliversToBothConsumers()
    {
        await FinalizeAsync(TestData.Invoice);

        (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(1);

        await AssertTrackSalesAsync(expectedPerTrack: 1);
        await AssertPurchaseSummaryAsync(expectedInvoices: 1);
        await using var orders = database.CreateOrdersContext();
        (await orders.Set<OutboxMessage>().SingleAsync()).ProcessedAt.Should().Be(_time.GetUtcNow());
        (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(0, "nothing is left to deliver");
    }

    [Fact]
    public async Task SameEventDeliveredTwice_UpdatesEachConsumerOnce()
    {
        await FinalizeAsync(TestData.Invoice);
        await Dispatcher.ProcessBatchAsync(CancellationToken.None);

        // At-least-once delivery: the same row comes round again, as after a crash before the commit.
        await using (var orders = database.CreateOrdersContext())
        {
            await orders.Set<OutboxMessage>().ExecuteUpdateAsync(set => set.SetProperty(m => m.ProcessedAt, (DateTimeOffset?)null));
        }

        (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(1);

        await AssertTrackSalesAsync(expectedPerTrack: 1);
        await AssertPurchaseSummaryAsync(expectedInvoices: 1);
        await using var catalog = database.CreateCatalogContext();
        (await catalog.Set<InboxMessage>().CountAsync()).Should().Be(1);
        await using var administration = database.CreateAdministrationContext();
        (await administration.Set<InboxMessage>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FailingConsumer_IsRetriedOnTheBackoffSchedule_ThenDeadLettered_AndTheOtherConsumerIsUnaffected()
    {
        await FinalizeAsync(TestData.Invoice);

        await using (await ForceFailureAsync("administration", "CustomerPurchaseSummary"))
        {
            // First delivery plus one retry per backoff step; each fails in Administration only.
            for (var delivery = 1; delivery <= OutboxDispatcher.RetryDelays.Count + 1; delivery++)
            {
                (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(1, "delivery {0} is due", delivery);
                if (delivery <= OutboxDispatcher.RetryDelays.Count)
                {
                    var delay = OutboxDispatcher.RetryDelays[delivery - 1];
                    _time.Advance(delay - TimeSpan.FromMilliseconds(1));
                    (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(0, "retry {0} waits {1}", delivery, delay);
                    _time.Advance(TimeSpan.FromMilliseconds(1));
                }
            }
        }

        await using (var orders = database.CreateOrdersContext())
        {
            var row = await orders.Set<OutboxMessage>().SingleAsync();
            row.Attempts.Should().Be(6);
            row.DeadLetteredAt.Should().NotBeNull();
            row.ProcessedAt.Should().BeNull();
            row.LastError.Should().Contain("Admin.Modules.Events.InvoiceFinalizedHandler");
        }

        _time.Advance(TimeSpan.FromHours(1));
        (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(0, "a dead letter waits for a person");
        await AssertTrackSalesAsync(expectedPerTrack: 1);
        await AssertPurchaseSummaryAsync(expectedInvoices: 0);
    }

    [Fact]
    public async Task RetryingADeadLetter_DeliversItToTheConsumerThatFailed()
    {
        await FinalizeAsync(TestData.Invoice);
        Guid id;
        await using (await ForceFailureAsync("administration", "CustomerPurchaseSummary"))
        {
            for (var delivery = 0; delivery <= OutboxDispatcher.RetryDelays.Count; delivery++)
            {
                await Dispatcher.ProcessBatchAsync(CancellationToken.None);
                _time.Advance(TimeSpan.FromHours(1));
            }

            await using var scope = _host.CreateAsyncScope();
            var deadLetters = await scope.ServiceProvider.GetRequiredService<DeadLetterService>().ListAsync(CancellationToken.None);
            id = deadLetters.Should().ContainSingle().Subject.Id;
        }

        await using (var scope = _host.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<DeadLetterService>().RetryAsync(id, CancellationToken.None)).Should().BeTrue();
        }

        (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(1);
        await AssertTrackSalesAsync(expectedPerTrack: 1);
        await AssertPurchaseSummaryAsync(expectedInvoices: 1);
    }

    private OrdersOutboxDispatcher Dispatcher => _host.GetRequiredService<OrdersOutboxDispatcher>();

    private async Task<FinalizeInvoiceResult> FinalizeAsync(int invoiceId)
    {
        await using var scope = _host.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IInvoiceService>().FinalizeInvoiceAsync(invoiceId, CancellationToken.None);
    }

    private async Task AssertTrackSalesAsync(int expectedPerTrack)
    {
        await using var catalog = database.CreateCatalogContext();
        var sales = await catalog.TrackSales.OrderBy(s => s.TrackId).ToListAsync();
        sales.Select(s => (s.TrackId, s.TimesSold)).Should().Equal(
            (TestData.Track1, expectedPerTrack), (TestData.Track2, expectedPerTrack));
    }

    private async Task AssertPurchaseSummaryAsync(int expectedInvoices)
    {
        await using var administration = database.CreateAdministrationContext();
        var summary = await administration.CustomerPurchaseSummaries.SingleOrDefaultAsync(s => s.CustomerId == TestData.Customer);
        if (expectedInvoices == 0)
        {
            summary.Should().BeNull();
            return;
        }

        summary!.InvoiceCount.Should().Be(expectedInvoices);
        summary.TotalSpent.Should().Be(2.28m * expectedInvoices);
    }

    /// <summary>Makes every insert or update on the table fail until disposed.</summary>
    private async Task<IAsyncDisposable> ForceFailureAsync(string schema, string table)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"""
            CREATE OR REPLACE FUNCTION {schema}.forced_failure() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN RAISE EXCEPTION 'forced failure for a test'; END $$;
            CREATE TRIGGER forced_failure BEFORE INSERT OR UPDATE ON {schema}."{table}"
            FOR EACH ROW EXECUTE FUNCTION {schema}.forced_failure();
            """, connection);
        await command.ExecuteNonQueryAsync();
        return new DropTrigger(database.ConnectionString, schema, table);
    }

    private sealed class DropTrigger(string connectionString, string schema, string table) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"DROP TRIGGER forced_failure ON {schema}.\"{table}\"; DROP FUNCTION {schema}.forced_failure();", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
