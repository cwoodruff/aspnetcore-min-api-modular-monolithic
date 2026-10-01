using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Module.Tests.Hosting;
using Npgsql;
using Orders.Contracts.Events;
using Orders.Modules.Domain;
using Orders.Modules.Services;
using SharedKernel.Events;

namespace ModularMonolith.Module.Tests.Orders;

/// <summary>
///     Orders' side of the InvoiceFinalized flow, in isolation: the outbox row written with the business change,
///     what the module publishes, and the dead-letter tools. Delivery semantics are in Kernel/OutboxDispatcherTests;
///     the consumers in their own hosts; the three together in Api.Tests.
/// </summary>
public sealed class OrdersOutboxTests(OrdersFixture database) : IClassFixture<OrdersFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAndSeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Finalize_WritesExactlyOneOutboxRow_WithTheInvoiceChange_AndPublishesTheEvent()
    {
        var publishedBefore = database.Host.Published.Count;

        (await FinalizeAsync(TestData.Invoice)).Should().Be(FinalizeInvoiceResult.Finalized);

        await using var orders = database.CreateOrdersContext();
        (await orders.Invoices.SingleAsync(i => i.Id == TestData.Invoice)).Status.Should().Be(InvoiceStatus.Finalized);
        var row = await orders.Set<OutboxMessage>().SingleAsync();
        row.EventType.Should().Be(typeof(InvoiceFinalized).FullName);
        using var payload = System.Text.Json.JsonDocument.Parse(row.Payload);
        payload.RootElement.GetProperty("invoiceId").GetInt32().Should().Be(TestData.Invoice);

        var published = database.Host.Published.Skip(publishedBefore).Should().ContainSingle().Subject
            .Should().BeOfType<InvoiceFinalized>().Subject;
        published.CustomerId.Should().Be(TestData.Customer);
        published.Lines.Select(line => line.TrackId).Should().Equal(TestData.Track1, TestData.Track2);
    }

    [Fact]
    public async Task Finalize_WhenTheOutboxWriteFails_RollsBackTheInvoiceChangeToo()
    {
        await using (await ForceFailureAsync("orders", "OutboxMessage"))
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
    public async Task ADeadLetter_IsListed_AndRetryingItSendsItBackForDelivery()
    {
        var id = Guid.NewGuid();
        await using (var orders = database.CreateOrdersContext())
        {
            orders.Set<OutboxMessage>().Add(new OutboxMessage
            {
                Id = id, EventType = typeof(InvoiceFinalized).FullName!, Payload = "{}", OccurredAt = DateTimeOffset.UtcNow,
                Attempts = 6, LastError = "consumer failed", DeadLetteredAt = DateTimeOffset.UtcNow,
                NextAttemptAt = DateTimeOffset.UtcNow
            });
            await orders.SaveChangesAsync();
        }

        await using (var scope = database.Host.CreateScope())
        {
            var deadLetters = scope.ServiceProvider.GetRequiredService<DeadLetterService>();
            (await deadLetters.ListAsync(CancellationToken.None)).Should().ContainSingle().Which.Id.Should().Be(id);
            (await deadLetters.RetryAsync(id, CancellationToken.None)).Should().BeTrue();
            (await deadLetters.ListAsync(CancellationToken.None)).Should().BeEmpty();
        }

        await using var check = database.CreateOrdersContext();
        var row = await check.Set<OutboxMessage>().SingleAsync();
        row.Attempts.Should().Be(0);
        row.DeadLetteredAt.Should().BeNull();
    }

    private async Task<FinalizeInvoiceResult> FinalizeAsync(int invoiceId)
    {
        await using var scope = database.Host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInvoiceService>().FinalizeInvoiceAsync(invoiceId, CancellationToken.None);
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
