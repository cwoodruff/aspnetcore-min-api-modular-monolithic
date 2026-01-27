# Outbox Pattern Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

The Outbox Pattern ensures reliable message publishing in distributed systems. It guarantees that database changes and message publishing happen atomically, preventing data inconsistencies when a service fails between saving data and publishing events.

**Duration:** 60-90 minutes
**Prerequisites:** Intermediate C#, understanding of message queues and transactions

---

## Learning Objectives

By the end of this guide, you will:
- Understand the dual-write problem
- Implement the Outbox Pattern
- Process outbox messages reliably
- Handle idempotency and retries
- Integrate with message brokers

---

## 1. The Dual-Write Problem

### The Problem

```
1. Save Order to Database     ✓ Success
2. Publish OrderCreated Event ✗ Failure (network issue)

Result: Order exists in DB, but no event was published
        → Other services never know about the order
        → Data inconsistency across services
```

### The Solution: Outbox Pattern

```
1. Begin Transaction
2. Save Order to Database
3. Save Event to Outbox Table
4. Commit Transaction
   (Both succeed or both fail atomically)

5. Background Process:
   - Read from Outbox
   - Publish to Message Broker
   - Mark as Published
```

---

## 2. Outbox Table Schema

```csharp
// Entities/OutboxMessage.cs
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
}

// EF Configuration
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Payload)
            .IsRequired();

        builder.HasIndex(x => x.ProcessedAt)
            .HasFilter("ProcessedAt IS NULL");

        builder.HasIndex(x => x.CreatedAt);
    }
}

// Migration
public partial class AddOutboxTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Type = table.Column<string>(maxLength: 256, nullable: false),
                Payload = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                ProcessedAt = table.Column<DateTime>(nullable: true),
                RetryCount = table.Column<int>(nullable: false, defaultValue: 0),
                Error = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_ProcessedAt",
            table: "OutboxMessages",
            column: "ProcessedAt",
            filter: "ProcessedAt IS NULL");
    }
}
```

---

## 3. Domain Events

### Event Base Class

```csharp
// Events/IDomainEvent.cs
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

// Events/OrderEvents.cs
public record OrderCreatedEvent(
    int OrderId,
    int CustomerId,
    decimal TotalAmount,
    IEnumerable<OrderItem> Items) : DomainEvent;

public record OrderItem(int ProductId, int Quantity, decimal UnitPrice);

public record OrderStatusChangedEvent(
    int OrderId,
    string OldStatus,
    string NewStatus) : DomainEvent;

public record OrderCancelledEvent(
    int OrderId,
    string Reason) : DomainEvent;
```

---

## 4. Outbox Service

```csharp
// Services/IOutboxService.cs
public interface IOutboxService
{
    Task SaveEventAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent;

    Task<IEnumerable<OutboxMessage>> GetUnprocessedMessagesAsync(
        int batchSize,
        CancellationToken ct = default);

    Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken ct = default);
}

// Services/OutboxService.cs
public class OutboxService : IOutboxService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OutboxService> _logger;

    public OutboxService(AppDbContext context, ILogger<OutboxService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveEventAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        var message = new OutboxMessage
        {
            Id = @event.EventId,
            Type = typeof(TEvent).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(@event),
            CreatedAt = @event.OccurredAt
        };

        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug("Saved outbox message: {Type} - {Id}", message.Type, message.Id);
    }

    public async Task<IEnumerable<OutboxMessage>> GetUnprocessedMessagesAsync(
        int batchSize,
        CancellationToken ct = default)
    {
        return await _context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        await _context.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.ProcessedAt, DateTime.UtcNow),
                ct);
    }

    public async Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        await _context.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                .SetProperty(m => m.Error, error),
                ct);
    }
}
```

---

## 5. Using Outbox in Business Logic

### Service with Outbox

```csharp
// Services/OrderService.cs
public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IOutboxService _outbox;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        AppDbContext context,
        IOutboxService outbox,
        ILogger<OrderService> logger)
    {
        _context = context;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        // Begin transaction (implicitly through EF)
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            // Create order
            var order = new Order
            {
                CustomerId = request.CustomerId,
                Status = "Pending",
                TotalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice),
                CreatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(ct);

            // Add order items
            foreach (var item in request.Items)
            {
                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });
            }

            await _context.SaveChangesAsync(ct);

            // Save event to outbox (same transaction)
            var @event = new OrderCreatedEvent(
                order.Id,
                order.CustomerId,
                order.TotalAmount,
                request.Items.Select(i => new Events.OrderItem(
                    i.ProductId, i.Quantity, i.UnitPrice)));

            await _outbox.SaveEventAsync(@event, ct);

            // Commit transaction
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Order {OrderId} created with outbox event", order.Id);

            return order;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task UpdateOrderStatusAsync(int orderId, string newStatus, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            var order = await _context.Orders.FindAsync(new object[] { orderId }, ct);
            if (order is null)
                throw new NotFoundException($"Order {orderId} not found");

            var oldStatus = order.Status;
            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            // Save status change event
            await _outbox.SaveEventAsync(
                new OrderStatusChangedEvent(orderId, oldStatus, newStatus),
                ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
```

---

## 6. Outbox Processor (Background Service)

```csharp
// Services/OutboxProcessor.cs
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly int _batchSize = 100;

    public OutboxProcessor(
        IServiceProvider services,
        ILogger<OutboxProcessor> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();

        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        var messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        var messages = await outboxService.GetUnprocessedMessagesAsync(_batchSize, ct);

        foreach (var message in messages)
        {
            try
            {
                // Deserialize event
                var eventType = Type.GetType(message.Type);
                if (eventType is null)
                {
                    _logger.LogWarning("Unknown event type: {Type}", message.Type);
                    await outboxService.MarkAsFailedAsync(message.Id, "Unknown event type", ct);
                    continue;
                }

                var @event = JsonSerializer.Deserialize(message.Payload, eventType);
                if (@event is null)
                {
                    _logger.LogWarning("Failed to deserialize event: {Id}", message.Id);
                    await outboxService.MarkAsFailedAsync(message.Id, "Deserialization failed", ct);
                    continue;
                }

                // Publish to message broker
                await messagePublisher.PublishAsync(@event, eventType, ct);

                // Mark as processed
                await outboxService.MarkAsProcessedAsync(message.Id, ct);

                _logger.LogDebug("Published outbox message: {Id}", message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message: {Id}", message.Id);
                await outboxService.MarkAsFailedAsync(message.Id, ex.Message, ct);
            }
        }
    }
}
```

---

## 7. Message Publisher (RabbitMQ Example)

```csharp
// Services/IMessagePublisher.cs
public interface IMessagePublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent;

    Task PublishAsync(object @event, Type eventType, CancellationToken ct = default);
}

// Services/RabbitMqPublisher.cs
public class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(
        IConfiguration config,
        ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"],
            UserName = config["RabbitMQ:Username"],
            Password = config["RabbitMQ:Password"]
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare exchange
        _channel.ExchangeDeclare(
            exchange: "domain-events",
            type: ExchangeType.Topic,
            durable: true);
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        return PublishAsync(@event, typeof(TEvent), ct);
    }

    public Task PublishAsync(object @event, Type eventType, CancellationToken ct = default)
    {
        var routingKey = eventType.Name; // e.g., "OrderCreatedEvent"
        var body = JsonSerializer.SerializeToUtf8Bytes(@event);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = eventType.AssemblyQualifiedName;
        properties.MessageId = (@event as IDomainEvent)?.EventId.ToString();

        _channel.BasicPublish(
            exchange: "domain-events",
            routingKey: routingKey,
            basicProperties: properties,
            body: body);

        _logger.LogDebug("Published {EventType} to RabbitMQ", eventType.Name);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
```

---

## 8. Idempotency

### Idempotent Message Handling

```csharp
// Consumers should track processed messages
public class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public DateTime ProcessedAt { get; set; }
}

public abstract class IdempotentEventHandler<TEvent>
    where TEvent : IDomainEvent
{
    private readonly AppDbContext _context;

    protected IdempotentEventHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task HandleAsync(TEvent @event, CancellationToken ct)
    {
        // Check if already processed
        var exists = await _context.ProcessedMessages
            .AnyAsync(p => p.MessageId == @event.EventId, ct);

        if (exists)
        {
            // Already processed, skip
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            // Process the event
            await ProcessEventAsync(@event, ct);

            // Mark as processed
            _context.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = @event.EventId,
                ProcessedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    protected abstract Task ProcessEventAsync(TEvent @event, CancellationToken ct);
}
```

---

## 9. Cleanup Old Messages

```csharp
// Services/OutboxCleanupService.cs
public class OutboxCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow - _retentionPeriod;

                var deleted = await context.OutboxMessages
                    .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} old outbox messages", deleted);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up outbox messages");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }
}
```

---

## 10. Alternative: Using MassTransit

```csharp
// MassTransit provides built-in outbox support
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UseSqlServer(); // or UsePostgres(), UseSqlite()
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

// In your service
public class OrderService
{
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        // ... create order

        // MassTransit handles outbox automatically
        await _publishEndpoint.Publish(new OrderCreatedEvent(/*...*/), ct);
    }
}
```

---

## 11. Testing

```csharp
public class OutboxServiceTests
{
    [Fact]
    public async Task SaveEventAsync_SavesMessageToDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;

        await using var context = new AppDbContext(options);
        var service = new OutboxService(context, Mock.Of<ILogger<OutboxService>>());

        var @event = new OrderCreatedEvent(1, 100, 99.99m, Array.Empty<Events.OrderItem>());

        // Act
        await service.SaveEventAsync(@event);

        // Assert
        var saved = await context.OutboxMessages.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal(@event.EventId, saved.Id);
        Assert.Null(saved.ProcessedAt);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_SetsProcessedAt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("TestDb2")
            .Options;

        await using var context = new AppDbContext(options);
        var messageId = Guid.NewGuid();

        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = messageId,
            Type = "Test",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new OutboxService(context, Mock.Of<ILogger<OutboxService>>());

        // Act
        await service.MarkAsProcessedAsync(messageId);

        // Assert
        var message = await context.OutboxMessages.FindAsync(messageId);
        Assert.NotNull(message?.ProcessedAt);
    }
}
```

---

## Summary

### Outbox Pattern Flow

```
1. Business Operation + Event → Same Transaction
2. Background Processor → Reads Outbox
3. Publish to Broker → Mark Processed
4. Cleanup → Remove Old Messages
```

### Best Practices

1. **Use transactions** — Save entity and outbox message atomically
2. **Handle idempotency** — Consumers should handle duplicates
3. **Retry with backoff** — Don't flood failing messages
4. **Clean up processed** — Prevent table bloat
5. **Monitor lag** — Alert if outbox grows too large
6. **Consider MassTransit** — Built-in outbox support

### When to Use

| Scenario | Use Outbox |
|----------|-----------|
| Microservices communication | Yes |
| Event-driven architecture | Yes |
| Saga orchestration | Yes |
| Simple monolith | Usually not needed |
| In-process events only | No |

### Files to Create

```
src/
├── Entities/
│   └── OutboxMessage.cs
├── Events/
│   ├── IDomainEvent.cs
│   └── OrderEvents.cs
├── Services/
│   ├── IOutboxService.cs
│   ├── OutboxService.cs
│   ├── IMessagePublisher.cs
│   └── RabbitMqPublisher.cs
└── BackgroundServices/
    ├── OutboxProcessor.cs
    └── OutboxCleanupService.cs
```
