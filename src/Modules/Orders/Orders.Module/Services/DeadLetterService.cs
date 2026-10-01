using Microsoft.EntityFrameworkCore;
using Orders.Modules.Data;
using SharedKernel.Events;

namespace Orders.Modules.Services;

/// <summary>
/// The two things an operator needs for the orders outbox: see what was dead-lettered, and send one
/// back for delivery. Nothing more (ADR-0008).
/// </summary>
internal sealed class DeadLetterService(OrdersDbContext db, TimeProvider time)
{
    public async Task<IReadOnlyList<DeadLetter>> ListAsync(CancellationToken ct)
    {
        return await db.Set<OutboxMessage>()
            .AsNoTracking()
            .Where(m => m.DeadLetteredAt != null)
            .OrderBy(m => m.DeadLetteredAt)
            .Select(m => new DeadLetter(m.Id, m.EventType, m.OccurredAt, m.Attempts, m.LastError, m.DeadLetteredAt!.Value))
            .ToListAsync(ct);
    }

    /// <returns>False if no dead-lettered message has this id.</returns>
    public async Task<bool> RetryAsync(Guid id, CancellationToken ct)
    {
        var message = await db.Set<OutboxMessage>().SingleOrDefaultAsync(m => m.Id == id && m.DeadLetteredAt != null, ct);
        if (message is null)
        {
            return false;
        }

        message.Attempts = 0;
        message.DeadLetteredAt = null;
        message.NextAttemptAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return true;
    }
}

internal sealed record DeadLetter(
    Guid Id,
    string EventType,
    DateTimeOffset OccurredAt,
    int Attempts,
    string? LastError,
    DateTimeOffset DeadLetteredAt);
