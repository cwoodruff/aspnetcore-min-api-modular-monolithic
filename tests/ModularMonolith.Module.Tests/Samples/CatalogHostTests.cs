using System.Net;
using System.Text.Json;
using Catalog.Host;
using Catalog.Modules.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Module.Tests.Hosting;
using Orders.Contracts.Events;
using SharedKernel;
using SharedKernel.Events;
using SharedKernel.Persistence;

namespace ModularMonolith.Module.Tests.Samples;

/// <summary>
///     samples/Catalog.Host: Catalog in a process of its own, fed from Orders' outbox by cursor (ADR-0017).
///     The database has Catalog's and Orders' schemas, seeded; Orders itself does not run.
/// </summary>
public sealed class CatalogHostTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _host = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        _connectionString = await PostgresServer.CreateDatabaseAsync(ModuleData.CatalogHost, seeded: true);
        _host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppDatabase"] = _connectionString,
                ["CatalogHost:Feed:Enabled"] = "false" // the tests run batches themselves
            }));
        });
        _ = _host.Server;
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private OrdersOutboxFeed Feed => _host.Services.GetRequiredService<OrdersOutboxFeed>();

    [Fact]
    public async Task ServesCatalogsHealth()
    {
        var health = await (await _host.CreateClient().GetAsync("/api/catalog/health")).ReadAsync(HttpStatusCode.OK);

        health.GetProperty("module").GetString().Should().Be("Catalog");
        health.GetProperty("service").GetString().Should().Be("Catalog.Host");
    }

    [Fact]
    public async Task ProtectedEndpoints_Return401_WithoutAToken() =>
        (await _host.CreateClient().GetAsync("/api/catalog/albums/1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task AFinalizedInvoice_IsCountedOnce_AndOrdersRowIsLeftForOrdersDispatcher()
    {
        var eventId = await WriteOutboxRowAsync(Finalized(trackId: 1), DateTimeOffset.UtcNow.AddMinutes(-1));

        (await Feed.ProcessBatchAsync(CancellationToken.None)).Should().Be(1);
        (await Feed.ProcessBatchAsync(CancellationToken.None)).Should().Be(0, "the cursor is past the row");
        await ResetCursorAsync();
        (await Feed.ProcessBatchAsync(CancellationToken.None)).Should().Be(1, "re-reading the log is allowed...");

        (await TimesSoldAsync(1)).Should().Be(1, "...and Catalog's inbox makes it take effect once");
        await using var connection = await OpenAsync();
        (await ScalarAsync<object>(connection, $"SELECT \"ProcessedAt\" FROM orders.\"OutboxMessage\" WHERE \"Id\" = '{eventId}'"))
            .Should().Be(DBNull.Value, "Orders' own dispatcher still owes it to Administration");
    }

    [Fact]
    public async Task ARowNewerThanTheSettleWindow_WaitsForTheNextPoll()
    {
        await WriteOutboxRowAsync(Finalized(trackId: 2), DateTimeOffset.UtcNow);

        (await Feed.ProcessBatchAsync(CancellationToken.None)).Should().Be(0);
        (await TimesSoldAsync(2)).Should().Be(0);
    }

    [Fact]
    public async Task AnEventCatalogDoesNotSubscribeTo_IsPassedOver()
    {
        await using (var connection = await OpenAsync())
        {
            await ExecuteAsync(connection, $"""
                INSERT INTO orders."OutboxMessage" ("Id", "EventType", "Payload", "OccurredAt", "Attempts", "NextAttemptAt")
                VALUES ('{Guid.NewGuid()}', 'Orders.Contracts.Events.SomethingElse', '{"{}"}', now() - interval '1 minute', 0, now())
                """);
        }

        (await Feed.ProcessBatchAsync(CancellationToken.None)).Should().Be(1);
    }

    private static InvoiceFinalized Finalized(int trackId) => new(Guid.NewGuid(), DateTimeOffset.UtcNow, 1, 1,
        new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 0.99m, [new InvoiceFinalizedLine(trackId, 1, 0.99m)]);

    // What Orders' publisher writes: the event as the shared JSON options serialize it.
    private async Task<Guid> WriteOutboxRowAsync(InvoiceFinalized finalized, DateTimeOffset occurredAt)
    {
        var json = _host.Services.GetRequiredKeyedService<JsonSerializerOptions>(ModuleJson.OptionsKey);
        var payload = JsonSerializer.Serialize(finalized, json.GetTypeInfo(typeof(InvoiceFinalized)));
        await using var connection = await OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("""
            INSERT INTO orders."OutboxMessage" ("Id", "EventType", "Payload", "OccurredAt", "Attempts", "NextAttemptAt")
            VALUES (@id, @type, @payload::jsonb, @at, 0, @at)
            """, connection);
        command.Parameters.AddWithValue("id", finalized.EventId);
        command.Parameters.AddWithValue("type", OutboxMessage.EventTypeName(typeof(InvoiceFinalized)));
        command.Parameters.AddWithValue("payload", payload);
        command.Parameters.AddWithValue("at", occurredAt);
        await command.ExecuteNonQueryAsync();
        return finalized.EventId;
    }

    private async Task ResetCursorAsync()
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection, "UPDATE catalog_host.orders_outbox_cursor SET occurred_at = '-infinity'");
    }

    private async Task<int> TimesSoldAsync(int trackId)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>();
        ModuleDbContextOptions.Use(options, _connectionString, CatalogDbContext.Schema);
        await using var catalog = new CatalogDbContext(options.Options);
        return await catalog.TrackSales.Where(s => s.TrackId == trackId).Select(s => s.TimesSold).SingleOrDefaultAsync();
    }

    private async Task<Npgsql.NpgsqlConnection> OpenAsync()
    {
        var connection = new Npgsql.NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteAsync(Npgsql.NpgsqlConnection connection, string sql)
    {
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T?> ScalarAsync<T>(Npgsql.NpgsqlConnection connection, string sql)
    {
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        return (T?)await command.ExecuteScalarAsync();
    }
}
