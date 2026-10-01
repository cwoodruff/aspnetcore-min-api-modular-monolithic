using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Events;

/// <summary>
/// Runs a handler at most once per event: inserts the inbox row first and skips if it already exists,
/// then saves the handler's work and commits both in one transaction on the handler module's context.
/// </summary>
public static class InboxGuard
{
    /// <returns>True if the handler ran; false if this handler had already handled the event.</returns>
    public static async Task<bool> RunAsync(DbContext handlerContext, Guid eventId, string handlerName,
        DateTimeOffset now, Func<CancellationToken, Task> handle, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(handlerContext);
        ArgumentNullException.ThrowIfNull(handle);

        var inbox = handlerContext.Model.FindEntityType(typeof(InboxMessage))
                    ?? throw new InvalidOperationException(
                        $"{handlerContext.GetType().Name} has no inbox; call modelBuilder.AddInbox().");
        var table = $"\"{inbox.GetSchema()}\".\"{inbox.GetTableName()}\"";
        var insert = "INSERT INTO " + table +
                     " (\"EventId\", \"HandlerName\", \"ProcessedAt\") VALUES ({0}, {1}, {2}) ON CONFLICT DO NOTHING";

        await using var transaction = await handlerContext.Database.BeginTransactionAsync(ct);
        var inserted = await handlerContext.Database.ExecuteSqlRawAsync(insert, [eventId, handlerName, now], ct);
        if (inserted == 0)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        await handle(ct);
        await handlerContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
