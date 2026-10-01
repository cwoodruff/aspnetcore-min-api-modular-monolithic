using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularMonolith.Module.Tests.Hosting;
using Npgsql;
using SharedKernel;
using SharedKernel.Concurrency;
using SharedKernel.Diagnostics;
using SharedKernel.Events;
using SharedKernel.Persistence;

namespace ModularMonolith.Module.Tests.Kernel;

/// <summary>
///     The outbox semantics of ADR-0008, tested on SharedKernel alone: a test publisher and two test consumers,
///     each with its own context and schema, no real module. (Orders' finalize flow, and Catalog's and
///     Administration's handlers, are tested in their own module hosts; the three together, in Api.Tests.)
/// </summary>
public sealed class OutboxDispatcherTests : IAsyncLifetime
{
    private readonly ManualTimeProvider _time = new();
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        var connectionString = await PostgresServer.CreateEmptyDatabaseAsync();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:AppDatabase"] = connectionString })
            .Build());
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_time);
        services.AddReflectionJsonSerialization();
        services.AddSingleton<FailureSwitch>();

        services.AddModuleDbContext<PublisherContext>("Publisher", PublisherContext.Schema);
        services.AddModuleDbContext<ConsumerAContext>("A", ConsumerAContext.Schema);
        services.AddModuleDbContext<ConsumerBContext>("B", ConsumerBContext.Schema);
        services.AddEventPublisher("Publisher");
        services.AddModuleWorkQueue("Publisher");
        services.AddSingleton<TestDispatcher>();
        services.AddIntegrationEventHandler<SomethingHappened, CountInA>("A");
        services.AddIntegrationEventHandler<SomethingHappened, CountInB>("B");
        _services = services.BuildServiceProvider();

        // Test contexts have no migrations; create their tables from the model.
        await using var scope = _services.CreateAsyncScope();
        foreach (var key in new[] { "Publisher", "A", "B" })
        {
            var context = scope.ServiceProvider.GetRequiredKeyedService<DbContext>(key);
            await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
        }
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    private TestDispatcher Dispatcher => _services.GetRequiredService<TestDispatcher>();

    [Fact]
    public async Task PublishedEvents_AreDeliveredToEveryHandler()
    {
        await PublishAsync(amount: 5);

        (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(1);

        (await CountAsync<ConsumerAContext>()).Should().Be(5);
        (await CountAsync<ConsumerBContext>()).Should().Be(5);
        (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(0, "nothing is left to deliver");
    }

    [Fact]
    public async Task AnEventDeliveredTwice_TakesEffectOncePerHandler()
    {
        await PublishAsync(amount: 5);
        await Dispatcher.ProcessBatchAsync(CancellationToken.None);

        // At-least-once: the same row comes round again, as after a crash before the commit.
        await using (var scope = _services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PublisherContext>().Set<OutboxMessage>()
                .ExecuteUpdateAsync(set => set.SetProperty(m => m.ProcessedAt, (DateTimeOffset?)null));
        }

        (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(1);

        (await CountAsync<ConsumerAContext>()).Should().Be(5);
        (await CountAsync<ConsumerBContext>()).Should().Be(5);
    }

    [Fact]
    public async Task AFailingHandler_IsRetriedOnTheBackoffSchedule_ThenDeadLettered_AndTheOtherIsUnaffected()
    {
        _services.GetRequiredService<FailureSwitch>().FailB = true;
        await PublishAsync(amount: 5);

        // First delivery plus one retry per backoff step; B fails every time.
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

        await using (var scope = _services.CreateAsyncScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<PublisherContext>().Set<OutboxMessage>().SingleAsync();
            row.Attempts.Should().Be(6);
            row.DeadLetteredAt.Should().NotBeNull();
            row.LastError.Should().Contain(nameof(CountInB));
        }

        _time.Advance(TimeSpan.FromHours(1));
        (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(0, "a dead letter waits for a person");
        (await CountAsync<ConsumerAContext>()).Should().Be(5, "A succeeded once and its inbox skipped every retry");
        (await CountAsync<ConsumerBContext>()).Should().Be(0);
    }

    [Fact]
    public async Task APublishIsWrittenOnlyWhenTheCallersTransactionCommits()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PublisherContext>();
        var publisher = scope.ServiceProvider.GetRequiredKeyedService<IEventPublisher>("Publisher");

        await publisher.PublishAsync(new SomethingHappened(Guid.NewGuid(), _time.GetUtcNow(), 1), db, CancellationToken.None);

        // Not saved: no row, nothing to deliver. There is no in-memory publish path.
        (await Dispatcher.ProcessBatchAsync(CancellationToken.None)).Should().Be(0);
    }

    private async Task PublishAsync(int amount)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PublisherContext>();
        await scope.ServiceProvider.GetRequiredKeyedService<IEventPublisher>("Publisher")
            .PublishAsync(new SomethingHappened(Guid.NewGuid(), _time.GetUtcNow(), amount), db, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    private async Task<int> CountAsync<TContext>() where TContext : ConsumerContext
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TContext>().Counters.SumAsync(c => c.Count);
    }

    public sealed record SomethingHappened(Guid EventId, DateTimeOffset OccurredAt, int Amount) : IIntegrationEvent;

    internal sealed class FailureSwitch
    {
        public bool FailB { get; set; }
    }

    internal sealed class Counter
    {
        public int Id { get; set; }
        public int Count { get; set; }
    }

    internal sealed class PublisherContext(DbContextOptions<PublisherContext> options) : DbContext(options)
    {
        public const string Schema = "kernel_publisher";

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema).AddOutbox();
    }

    internal abstract class ConsumerContext(DbContextOptions options, string schema) : DbContext(options)
    {
        public DbSet<Counter> Counters => Set<Counter>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(schema).AddInbox();
            modelBuilder.Entity<Counter>().ToTable("Counter");
        }
    }

    internal sealed class ConsumerAContext(DbContextOptions<ConsumerAContext> options) : ConsumerContext(options, Schema)
    {
        public const string Schema = "kernel_a";
    }

    internal sealed class ConsumerBContext(DbContextOptions<ConsumerBContext> options) : ConsumerContext(options, Schema)
    {
        public const string Schema = "kernel_b";
    }

    internal sealed class CountInA(ConsumerAContext db) : IIntegrationEventHandler<SomethingHappened>
    {
        public Task HandleAsync(SomethingHappened integrationEvent, CancellationToken ct)
        {
            db.Counters.Add(new Counter { Count = integrationEvent.Amount });
            return Task.CompletedTask;
        }
    }

    internal sealed class CountInB(ConsumerBContext db, FailureSwitch failure) : IIntegrationEventHandler<SomethingHappened>
    {
        public Task HandleAsync(SomethingHappened integrationEvent, CancellationToken ct)
        {
            if (failure.FailB)
            {
                throw new InvalidOperationException("B is failing on purpose.");
            }

            db.Counters.Add(new Counter { Count = integrationEvent.Amount });
            return Task.CompletedTask;
        }
    }

    internal sealed class TestDispatcher(
        IServiceScopeFactory scopes,
        IEnumerable<IntegrationEventSubscription> subscriptions,
        [FromKeyedServices(ModuleJson.OptionsKey)] System.Text.Json.JsonSerializerOptions json,
        IConfiguration configuration,
        TimeProvider time,
        [FromKeyedServices("Publisher")] ModuleWorkQueue workQueue,
        [FromKeyedServices("Publisher")] ModuleMeter meter,
        ILogger<TestDispatcher> logger)
        : OutboxDispatcher<PublisherContext>(scopes, subscriptions, json, configuration, time, workQueue, meter, logger)
    {
        protected override IReadOnlyCollection<Type> EventTypes { get; } = [typeof(SomethingHappened)];
    }
}
