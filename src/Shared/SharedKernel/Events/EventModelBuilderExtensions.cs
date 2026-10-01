using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Events;

/// <summary>
/// Maps the outbox and inbox tables into the calling context's default schema, so each module keeps
/// its own.
/// </summary>
public static class EventModelBuilderExtensions
{
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessage");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.EventType).HasColumnType("varchar(300)");
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            entity.Property(e => e.LastError).HasColumnType("varchar(2000)");

            // What the dispatcher polls: undelivered, not dead-lettered, due.
            entity.HasIndex(e => new { e.NextAttemptAt, e.OccurredAt })
                .HasFilter("\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL");
            entity.HasIndex(e => e.DeadLetteredAt).HasFilter("\"DeadLetteredAt\" IS NOT NULL");
        });
        return modelBuilder;
    }

    public static ModelBuilder AddInbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessage");
            entity.HasKey(e => new { e.EventId, e.HandlerName });
            entity.Property(e => e.HandlerName).HasColumnType("varchar(300)");
        });
        return modelBuilder;
    }
}
