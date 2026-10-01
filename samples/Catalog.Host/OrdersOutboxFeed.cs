using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using SharedKernel.Events;

namespace Catalog.Host;

/// <summary>
///     Delivers the events Catalog subscribes to from Orders' outbox table, without touching it (ADR-0017).
///     Orders' own dispatcher marks a row done when the handlers in its process have run; a second process
///     claiming rows would take them away from Administration. So this reads the table as a log instead:
///     in (OccurredAt, Id) order, past a cursor it keeps in its own schema, through Catalog's inbox, so an
///     event Catalog already handled in the monolith takes effect once.
/// </summary>
/// <remarks>
///     Only rows older than <c>CatalogHost:Feed:SettleSeconds</c> are read: OccurredAt is stamped before the
///     publishing transaction commits, so a row can appear behind the cursor. The window must exceed the
///     longest such transaction; a broker removes the problem. A handler failure stops the batch at that
///     row, which is retried on the next poll. There is no dead-lettering here.
/// </remarks>
public sealed class OrdersOutboxFeed(
    IServiceScopeFactory scopes,
    IEnumerable<IntegrationEventSubscription> subscriptions,
    [FromKeyedServices(ModuleJson.OptionsKey)] JsonSerializerOptions json,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<OrdersOutboxFeed> logger) : BackgroundService
{
    public const int BatchSize = 50;
    private const string ModuleKey = "Catalog";

    private readonly IntegrationEventSubscription[] _subscriptions = [.. subscriptions.Where(s => s.ModuleKey == ModuleKey)];
    private readonly TimeSpan _settle = TimeSpan.FromSeconds(configuration.GetValue("CatalogHost:Feed:SettleSeconds", 5.0));

    /// <summary>Delivers up to <see cref="BatchSize" /> settled rows past the cursor; returns how many it moved past.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredKeyedService<DbContext>(ModuleKey);
        await EnsureCursorAsync(db, ct);

        var cursor = await db.Database.SqlQueryRaw<Cursor>(
            "SELECT occurred_at AS \"OccurredAt\", event_id AS \"EventId\" FROM catalog_host.orders_outbox_cursor WHERE id = 1")
            .SingleAsync(ct);
        var rows = await db.Database.SqlQueryRaw<Row>(
                "SELECT \"Id\", \"EventType\", \"Payload\"::text AS \"Payload\", \"OccurredAt\" FROM orders.\"OutboxMessage\"" +
                " WHERE (\"OccurredAt\", \"Id\") > ({0}, {1}) AND \"OccurredAt\" <= {2}" +
                " ORDER BY \"OccurredAt\", \"Id\" LIMIT 50", // BatchSize
                cursor.OccurredAt, cursor.EventId, time.GetUtcNow() - _settle)
            .ToListAsync(ct);

        var moved = 0;
        foreach (var row in rows)
        {
            if (!await DeliverAsync(row, ct))
            {
                break;
            }

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE catalog_host.orders_outbox_cursor SET occurred_at = {0}, event_id = {1} WHERE id = 1",
                [row.OccurredAt, row.Id], ct);
            moved++;
        }

        return moved;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("CatalogHost:Feed:Enabled", true))
        {
            return;
        }

        var poll = TimeSpan.FromSeconds(configuration.GetValue("CatalogHost:Feed:PollIntervalSeconds", 1.0));
        while (!stoppingToken.IsCancellationRequested)
        {
            var moved = 0;
            try
            {
                moved = await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // The loop must survive a failed poll (database down, etc.) and try again.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                FeedLog.PollFailed(logger, ex);
            }

            if (moved < BatchSize)
            {
                try
                {
                    await Task.Delay(poll, time, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    /// <returns>False if a handler failed; the cursor stays before this row.</returns>
    private async Task<bool> DeliverAsync(Row row, CancellationToken ct)
    {
        var handlers = _subscriptions.Where(s => OutboxMessage.EventTypeName(s.EventType) == row.EventType).ToArray();
        if (handlers.Length == 0)
        {
            return true; // An event Catalog does not subscribe to.
        }

        var integrationEvent = (IIntegrationEvent)JsonSerializer.Deserialize(row.Payload, json.GetTypeInfo(handlers[0].EventType))!;
        foreach (var subscription in handlers)
        {
            // Same handler name as in the monolith, so both processes share one inbox entry per event.
            var handlerName = subscription.HandlerType.FullName ?? subscription.HandlerType.Name;
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await InboxGuard.RunAsync(scope.ServiceProvider.GetRequiredKeyedService<DbContext>(ModuleKey), row.Id, handlerName,
                    time.GetUtcNow(), token => subscription.Handle(scope.ServiceProvider, integrationEvent, token), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // A failed handler holds the cursor; the row is retried on the next poll.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                FeedLog.HandlerFailed(logger, handlerName, row.Id, ex);
                return false;
            }
        }

        return true;
    }

    private static Task<int> EnsureCursorAsync(DbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync(
            """
            CREATE SCHEMA IF NOT EXISTS catalog_host;
            CREATE TABLE IF NOT EXISTS catalog_host.orders_outbox_cursor (
                id int PRIMARY KEY, occurred_at timestamptz NOT NULL, event_id uuid NOT NULL);
            INSERT INTO catalog_host.orders_outbox_cursor VALUES (1, '-infinity', '00000000-0000-0000-0000-000000000000')
            ON CONFLICT (id) DO NOTHING;
            """, ct);

    private sealed record Cursor(DateTimeOffset OccurredAt, Guid EventId);

    private sealed record Row(Guid Id, string EventType, string Payload, DateTimeOffset OccurredAt);
}

public static class OrdersOutboxFeedRegistration
{
    public static IServiceCollection AddOrdersOutboxFeed(this IServiceCollection services)
    {
        services.AddSingleton<OrdersOutboxFeed>();
        services.AddHostedService(sp => sp.GetRequiredService<OrdersOutboxFeed>());
        return services;
    }
}

internal static partial class FeedLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Orders outbox feed poll failed")]
    public static partial void PollFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "Handler {Handler} failed for event {EventId}; the feed will retry it")]
    public static partial void HandlerFailed(ILogger logger, string handler, Guid eventId, Exception exception);
}
