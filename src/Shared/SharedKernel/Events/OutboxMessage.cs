using System.Text.Json;

namespace SharedKernel.Events;

/// <summary>
/// One published event waiting in, or delivered from, a module's outbox table. Each publishing module
/// maps it into its own schema with <see cref="EventModelBuilderExtensions.AddOutbox" />.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>The event's EventId.</summary>
    public Guid Id { get; set; }

    /// <summary>The event's CLR type full name; the dispatcher maps it back to a type it knows.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The event as JSON (jsonb).</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Set once every handler has handled the event.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Failed deliveries so far.</summary>
    public int Attempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>Set when the retry schedule is exhausted; the row is then left alone until retried by hand.</summary>
    public DateTimeOffset? DeadLetteredAt { get; set; }

    /// <summary>When the dispatcher may next deliver this row (retry backoff).</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    public static string EventTypeName(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return eventType.FullName ?? eventType.Name;
    }
}

/// <summary>Records that one handler has handled one event, in the handler module's schema.</summary>
public sealed class InboxMessage
{
    public Guid EventId { get; set; }

    public string HandlerName { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; set; }
}

internal static class IntegrationEventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<TEvent>(TEvent integrationEvent) => JsonSerializer.Serialize(integrationEvent, Options);

    public static object? Deserialize(string payload, Type eventType) => JsonSerializer.Deserialize(payload, eventType, Options);
}
