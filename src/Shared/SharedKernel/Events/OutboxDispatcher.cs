using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Concurrency;
using SharedKernel.Diagnostics;

namespace SharedKernel.Events;

/// <summary>
/// Delivers one module's outbox to every registered handler (ADR-0008). The hosted loop runs
/// <see cref="ProcessBatchAsync" /> through the module's <see cref="ModuleWorkQueue" />; tests call it
/// directly instead of waiting for the loop. Counts go to the module's <see cref="ModuleMeter" />.
/// </summary>
/// <remarks>
/// Semantics, chosen on purpose:
/// <list type="bullet">
///   <item>At least once. Each handler is guarded by its module's inbox, so it takes effect once.</item>
///   <item>Handlers are found by module key (<see cref="EventServiceCollectionExtensions.AddIntegrationEventHandler{TEvent,THandler}" />),
///   and each runs on the DbContext registered under the same key.</item>
///   <item>A row is done when every handler has succeeded; a handler that already succeeded is skipped
///   by its inbox when the row is retried.</item>
///   <item>Retries after 1s, 5s, 30s, 2m and 10m. The sixth failed delivery dead-letters the row, which
///   then waits for someone to retry it by hand.</item>
///   <item>Ordering holds only within one dispatcher, in the order rows were written (OccurredAt, then
///   Id), and only until a row fails: a retried row is delivered after rows written later. There is
///   no ordering across modules.</item>
/// </list>
/// </remarks>
public abstract class OutboxDispatcher(
    IConfiguration configuration,
    TimeProvider time,
    ModuleWorkQueue workQueue,
    ModuleMeter meter,
    ILogger logger)
    : BackgroundService
{
    public const int BatchSize = 50;

    /// <summary>Delay before each retry; one entry per retry.</summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
    [
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)
    ];

    protected TimeProvider Time { get; } = time;

    /// <summary>The publishing module's meter: dispatched, retried and dead-lettered events.</summary>
    protected ModuleMeter Meter { get; } = meter;

    protected ILogger Logger { get; } = logger;

    /// <summary>Delivers up to <see cref="BatchSize" /> due rows; returns how many rows it looked at.</summary>
    public abstract Task<int> ProcessBatchAsync(CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Outbox:Enabled", true))
        {
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(configuration.GetValue("Outbox:PollIntervalSeconds", 1.0));
        while (!stoppingToken.IsCancellationRequested)
        {
            var handled = 0;
            try
            {
                // Through the module's bounded queue, so its background work runs one item at a time (ADR-0013).
                handled = await workQueue.RunAsync(ProcessBatchAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // The loop must survive a failed poll (database down, etc.) and try again.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                OutboxLog.PollFailed(Logger, GetType().Name, ex);
            }

            if (handled < BatchSize)
            {
                try
                {
                    await Task.Delay(pollInterval, Time, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}

/// <summary>Dispatcher for the outbox in <typeparamref name="TContext" />'s schema.</summary>
public abstract class OutboxDispatcher<TContext>(
    IServiceScopeFactory scopes,
    IEnumerable<IntegrationEventSubscription> subscriptions,
    JsonSerializerOptions json,
    IConfiguration configuration,
    TimeProvider time,
    ModuleWorkQueue workQueue,
    ModuleMeter meter,
    ILogger logger) : OutboxDispatcher(configuration, time, workQueue, meter, logger)
    where TContext : DbContext
{
    private readonly IntegrationEventSubscription[] _subscriptions = [.. subscriptions];
    private readonly JsonSerializerOptions _json = json;
    private Dictionary<string, Type>? _eventTypes;

    /// <summary>The event types this module publishes.</summary>
    protected abstract IReadOnlyCollection<Type> EventTypes { get; }

    public override async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        _eventTypes ??= EventTypes.ToDictionary(OutboxMessage.EventTypeName);

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var outbox = db.Model.FindEntityType(typeof(OutboxMessage))
                     ?? throw new InvalidOperationException($"{typeof(TContext).Name} has no outbox; call modelBuilder.AddOutbox().");
        var table = $"\"{outbox.GetSchema()}\".\"{outbox.GetTableName()}\"";
        var due = "SELECT * FROM " + table +
                  " WHERE \"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL AND \"NextAttemptAt\" <= {0}" +
                  " ORDER BY \"OccurredAt\", \"Id\" LIMIT " + BatchSize + " FOR UPDATE SKIP LOCKED";

        // The row locks are held until commit, so a second dispatcher skips these rows.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var batch = await db.Set<OutboxMessage>().FromSqlRaw(due, Time.GetUtcNow()).ToListAsync(ct);

        foreach (var message in batch)
        {
            var error = await DeliverAsync(message, ct);
            var now = Time.GetUtcNow();
            if (error is null)
            {
                message.ProcessedAt = now;
                Meter.Dispatched();
                continue;
            }

            message.Attempts++;
            message.LastError = error.Length > 2000 ? error[..2000] : error;
            if (message.Attempts > RetryDelays.Count)
            {
                message.DeadLetteredAt = now;
                Meter.DeadLettered();
                OutboxLog.DeadLettered(Logger, message.Id, message.EventType, message.Attempts);
            }
            else
            {
                message.NextAttemptAt = now + RetryDelays[message.Attempts - 1];
                Meter.Retried();
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return batch.Count;
    }

    /// <returns>Null when every handler succeeded (or had already), otherwise the failures.</returns>
    private async Task<string?> DeliverAsync(OutboxMessage message, CancellationToken ct)
    {
        if (!_eventTypes!.TryGetValue(message.EventType, out var eventType))
        {
            return $"Unknown event type '{message.EventType}'.";
        }

        IIntegrationEvent integrationEvent;
        try
        {
            integrationEvent = (IIntegrationEvent)(JsonSerializer.Deserialize(message.Payload, _json.GetTypeInfo(eventType))
                                                   ?? throw new InvalidOperationException("Payload deserialized to null."));
        }
#pragma warning disable CA1031 // A payload that cannot be read is a delivery failure, recorded on the row.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return $"Cannot read payload: {ex.Message}";
        }

        var failures = new List<string>();
        foreach (var subscription in _subscriptions.Where(s => s.EventType == eventType))
        {
            // A scope per handler: each gets its own module context and transaction.
            await using var handlerScope = scopes.CreateAsyncScope();
            var handlerName = subscription.HandlerType.FullName ?? subscription.HandlerType.Name;
            try
            {
                // The inbox lives in the handler's own module: the context registered under the same key.
                var handlerContext = handlerScope.ServiceProvider.GetRequiredKeyedService<DbContext>(subscription.ModuleKey);
                await InboxGuard.RunAsync(handlerContext, message.Id, handlerName, Time.GetUtcNow(),
                    token => subscription.Handle(handlerScope.ServiceProvider, integrationEvent, token),
                    ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // One handler failing must not stop the others; the failure goes on the row.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                OutboxLog.HandlerFailed(Logger, handlerName, message.Id, ex);
                failures.Add($"{handlerName}: {ex.GetBaseException().Message}");
            }
        }

        return failures.Count == 0 ? null : string.Join(Environment.NewLine, failures);
    }
}

internal static partial class OutboxLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Outbox poll failed in {Dispatcher}")]
    public static partial void PollFailed(ILogger logger, string dispatcher, Exception exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Handler {Handler} failed for event {EventId}")]
    public static partial void HandlerFailed(ILogger logger, string handler, Guid eventId, Exception exception);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error,
        Message = "Outbox message {EventId} ({EventType}) dead-lettered after {Attempts} failed deliveries")]
    public static partial void DeadLettered(ILogger logger, Guid eventId, string eventType, int attempts);
}
