# Background Services Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

Background services allow your API to perform work asynchronously without blocking HTTP requests. Common use cases include queue processing, scheduled tasks, cache warming, and cleanup jobs.

**Duration:** 45-60 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of async/await

---

## Learning Objectives

By the end of this guide, you will:
- Implement IHostedService and BackgroundService
- Create scheduled background tasks
- Process queued work items
- Handle graceful shutdown
- Monitor background service health

---

## 1. Understanding Background Services

### Service Types

| Type | Use Case | Lifecycle |
|------|----------|-----------|
| `IHostedService` | Full control over start/stop | Manual implementation |
| `BackgroundService` | Long-running tasks | Built-in loop handling |
| Timed Service | Periodic tasks | Timer-based execution |
| Queue Processor | Work item processing | Consumer pattern |

### Lifecycle

```
Application Start
       │
       ▼
┌──────────────────┐
│ StartAsync()     │ ◄── Called once at startup
└──────────────────┘
       │
       ▼
┌──────────────────┐
│ ExecuteAsync()   │ ◄── Long-running work (BackgroundService)
│ (running...)     │
└──────────────────┘
       │
       ▼ (shutdown signal)
┌──────────────────┐
│ StopAsync()      │ ◄── Called once at shutdown
└──────────────────┘
       │
       ▼
Application Stop
```

---

## 2. Basic BackgroundService

### Simple Implementation

```csharp
// Services/Background/SimpleBackgroundService.cs
public class SimpleBackgroundService : BackgroundService
{
    private readonly ILogger<SimpleBackgroundService> _logger;

    public SimpleBackgroundService(ILogger<SimpleBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background service running at: {Time}", DateTime.UtcNow);

            try
            {
                await DoWorkAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in background service");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Background service stopping");
    }

    private async Task DoWorkAsync(CancellationToken ct)
    {
        // Your background work here
        await Task.CompletedTask;
    }
}

// Registration
builder.Services.AddHostedService<SimpleBackgroundService>();
```

---

## 3. Timed Background Service

### Periodic Task Execution

```csharp
// Services/Background/TimedCleanupService.cs
public class TimedCleanupService : BackgroundService
{
    private readonly ILogger<TimedCleanupService> _logger;
    private readonly IServiceProvider _services;
    private readonly TimeSpan _interval;

    public TimedCleanupService(
        ILogger<TimedCleanupService> logger,
        IServiceProvider services,
        IConfiguration config)
    {
        _logger = logger;
        _services = services;
        _interval = TimeSpan.FromMinutes(
            config.GetValue<int>("Cleanup:IntervalMinutes", 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Cleanup service started. Running every {Interval}",
            _interval);

        // Use PeriodicTimer for accurate intervals
        using var timer = new PeriodicTimer(_interval);

        // Run immediately on startup, then on interval
        await RunCleanupAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting cleanup at {Time}", DateTime.UtcNow);

        try
        {
            // Create scope for scoped services
            using var scope = _services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Example: Clean up expired sessions
            var expiredDate = DateTime.UtcNow.AddDays(-30);
            var deleted = await dbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM ExpiredTokens WHERE ExpiresAt < {expiredDate}",
                    ct);

            _logger.LogInformation("Cleanup completed. Deleted {Count} expired records", deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup failed");
        }
    }
}
```

---

## 4. Scheduled Task Service (Cron-like)

```csharp
// Services/Background/ScheduledTaskService.cs
public class ScheduledTaskService : BackgroundService
{
    private readonly ILogger<ScheduledTaskService> _logger;
    private readonly IServiceProvider _services;
    private readonly List<ScheduledTask> _tasks;

    public ScheduledTaskService(
        ILogger<ScheduledTaskService> logger,
        IServiceProvider services)
    {
        _logger = logger;
        _services = services;

        _tasks = new List<ScheduledTask>
        {
            new("Daily Report", TimeOnly.Parse("06:00"), GenerateDailyReportAsync),
            new("Hourly Stats", null, UpdateHourlyStatsAsync, TimeSpan.FromHours(1)),
            new("Cache Warmup", TimeOnly.Parse("05:00"), WarmupCacheAsync)
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduled task service started with {Count} tasks", _tasks.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            foreach (var task in _tasks)
            {
                if (task.ShouldRun(now))
                {
                    _ = RunTaskAsync(task, stoppingToken);
                }
            }

            // Check every minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task RunTaskAsync(ScheduledTask task, CancellationToken ct)
    {
        _logger.LogInformation("Running scheduled task: {TaskName}", task.Name);

        try
        {
            using var scope = _services.CreateScope();
            await task.Action(scope.ServiceProvider, ct);
            task.MarkCompleted();

            _logger.LogInformation("Scheduled task completed: {TaskName}", task.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled task failed: {TaskName}", task.Name);
        }
    }

    private async Task GenerateDailyReportAsync(IServiceProvider services, CancellationToken ct)
    {
        var reportService = services.GetRequiredService<IReportService>();
        await reportService.GenerateDailyReportAsync(ct);
    }

    private async Task UpdateHourlyStatsAsync(IServiceProvider services, CancellationToken ct)
    {
        var statsService = services.GetRequiredService<IStatsService>();
        await statsService.UpdateStatsAsync(ct);
    }

    private async Task WarmupCacheAsync(IServiceProvider services, CancellationToken ct)
    {
        var cacheService = services.GetRequiredService<ICacheWarmupService>();
        await cacheService.WarmupAsync(ct);
    }
}

// Helper class for scheduled tasks
public class ScheduledTask
{
    public string Name { get; }
    public TimeOnly? ScheduledTime { get; }
    public TimeSpan? Interval { get; }
    public Func<IServiceProvider, CancellationToken, Task> Action { get; }

    private DateTime _lastRun = DateTime.MinValue;

    public ScheduledTask(
        string name,
        TimeOnly? scheduledTime,
        Func<IServiceProvider, CancellationToken, Task> action,
        TimeSpan? interval = null)
    {
        Name = name;
        ScheduledTime = scheduledTime;
        Action = action;
        Interval = interval;
    }

    public bool ShouldRun(DateTime now)
    {
        // Interval-based task
        if (Interval.HasValue)
        {
            return now - _lastRun >= Interval.Value;
        }

        // Time-based task (run once per day at scheduled time)
        if (ScheduledTime.HasValue)
        {
            var scheduledDateTime = now.Date.Add(ScheduledTime.Value.ToTimeSpan());
            return now >= scheduledDateTime && _lastRun.Date < now.Date;
        }

        return false;
    }

    public void MarkCompleted()
    {
        _lastRun = DateTime.UtcNow;
    }
}
```

---

## 5. Queue-Based Background Processing

### Background Task Queue

```csharp
// Services/Background/BackgroundTaskQueue.cs
public interface IBackgroundTaskQueue
{
    ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem);
    ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(
        CancellationToken cancellationToken);
}

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, ValueTask>> _queue;

    public BackgroundTaskQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, ValueTask>>(options);
    }

    public async ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(
        CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}

// Registration
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
```

### Queue Processor Service

```csharp
// Services/Background/QueuedHostedService.cs
public class QueuedHostedService : BackgroundService
{
    private readonly ILogger<QueuedHostedService> _logger;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceProvider _services;

    public QueuedHostedService(
        ILogger<QueuedHostedService> logger,
        IBackgroundTaskQueue taskQueue,
        IServiceProvider services)
    {
        _logger = logger;
        _taskQueue = taskQueue;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                using var scope = _services.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued work item");
            }
        }

        _logger.LogInformation("Queue processor stopping");
    }
}

// Registration
builder.Services.AddHostedService<QueuedHostedService>();
```

### Using the Queue

```csharp
// In an endpoint or service
app.MapPost("/api/orders", async (
    OrderRequest request,
    IOrderService orderService,
    IBackgroundTaskQueue taskQueue,
    CancellationToken ct) =>
{
    // Create order synchronously
    var order = await orderService.CreateAsync(request, ct);

    // Queue background work
    await taskQueue.QueueAsync(async (services, ct) =>
    {
        var emailService = services.GetRequiredService<IEmailService>();
        await emailService.SendOrderConfirmationAsync(order.Id, ct);
    });

    await taskQueue.QueueAsync(async (services, ct) =>
    {
        var inventoryService = services.GetRequiredService<IInventoryService>();
        await inventoryService.UpdateStockAsync(order.Items, ct);
    });

    return TypedResults.Created($"/api/orders/{order.Id}", order);
});
```

---

## 6. Parallel Queue Processing

```csharp
// Services/Background/ParallelQueueProcessor.cs
public class ParallelQueueProcessor : BackgroundService
{
    private readonly ILogger<ParallelQueueProcessor> _logger;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceProvider _services;
    private readonly int _workerCount;

    public ParallelQueueProcessor(
        ILogger<ParallelQueueProcessor> logger,
        IBackgroundTaskQueue taskQueue,
        IServiceProvider services,
        IConfiguration config)
    {
        _logger = logger;
        _taskQueue = taskQueue;
        _services = services;
        _workerCount = config.GetValue<int>("BackgroundQueue:WorkerCount", 3);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting {WorkerCount} parallel queue workers",
            _workerCount);

        var workers = Enumerable
            .Range(0, _workerCount)
            .Select(i => ProcessQueueAsync(i, stoppingToken))
            .ToArray();

        await Task.WhenAll(workers);
    }

    private async Task ProcessQueueAsync(int workerId, CancellationToken ct)
    {
        _logger.LogDebug("Worker {WorkerId} started", workerId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(ct);

                _logger.LogDebug("Worker {WorkerId} processing item", workerId);

                using var scope = _services.CreateScope();
                await workItem(scope.ServiceProvider, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} error", workerId);
            }
        }

        _logger.LogDebug("Worker {WorkerId} stopped", workerId);
    }
}
```

---

## 7. Cache Warmup Service

```csharp
// Services/Background/CacheWarmupService.cs
public class CacheWarmupService : BackgroundService
{
    private readonly ILogger<CacheWarmupService> _logger;
    private readonly IServiceProvider _services;
    private readonly StartupHealthCheck _healthCheck;

    public CacheWarmupService(
        ILogger<CacheWarmupService> logger,
        IServiceProvider services,
        StartupHealthCheck healthCheck)
    {
        _logger = logger;
        _services = services;
        _healthCheck = healthCheck;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting cache warmup");

        try
        {
            using var scope = _services.CreateScope();

            // Warm up frequently accessed data
            var tasks = new[]
            {
                WarmupGenresAsync(scope.ServiceProvider, stoppingToken),
                WarmupArtistsAsync(scope.ServiceProvider, stoppingToken),
                WarmupPopularAlbumsAsync(scope.ServiceProvider, stoppingToken)
            };

            await Task.WhenAll(tasks);

            _healthCheck.IsReady = true;
            _logger.LogInformation("Cache warmup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache warmup failed");
            // Don't throw - let app start anyway
            _healthCheck.IsReady = true;
        }
    }

    private async Task WarmupGenresAsync(IServiceProvider services, CancellationToken ct)
    {
        _logger.LogDebug("Warming up genres cache");
        var service = services.GetRequiredService<IGenreService>();
        await service.GetAllAsync(ct);
    }

    private async Task WarmupArtistsAsync(IServiceProvider services, CancellationToken ct)
    {
        _logger.LogDebug("Warming up artists cache");
        var service = services.GetRequiredService<IArtistService>();
        await service.GetAllAsync(ct);
    }

    private async Task WarmupPopularAlbumsAsync(IServiceProvider services, CancellationToken ct)
    {
        _logger.LogDebug("Warming up popular albums cache");
        var service = services.GetRequiredService<IAlbumService>();
        await service.GetPopularAsync(limit: 100, ct);
    }
}
```

---

## 8. Graceful Shutdown

```csharp
// Services/Background/GracefulBackgroundService.cs
public class GracefulBackgroundService : BackgroundService
{
    private readonly ILogger<GracefulBackgroundService> _logger;
    private int _activeWorkItems = 0;

    public GracefulBackgroundService(ILogger<GracefulBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            Interlocked.Increment(ref _activeWorkItems);

            try
            {
                await ProcessWorkItemAsync(stoppingToken);
            }
            finally
            {
                Interlocked.Decrement(ref _activeWorkItems);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Shutdown requested. Waiting for {Count} active work items",
            _activeWorkItems);

        // Wait for active work to complete (with timeout)
        var timeout = TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();

        while (_activeWorkItems > 0 && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(100, cancellationToken);
        }

        if (_activeWorkItems > 0)
        {
            _logger.LogWarning(
                "Shutdown timeout. {Count} work items still active",
                _activeWorkItems);
        }
        else
        {
            _logger.LogInformation("All work items completed");
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessWorkItemAsync(CancellationToken ct)
    {
        // Simulate work
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
    }
}
```

---

## 9. Health Check Integration

```csharp
// HealthChecks/BackgroundServiceHealthCheck.cs
public class BackgroundServiceHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IHostedService> _hostedServices;
    private readonly ILogger<BackgroundServiceHealthCheck> _logger;

    public BackgroundServiceHealthCheck(
        IEnumerable<IHostedService> hostedServices,
        ILogger<BackgroundServiceHealthCheck> logger)
    {
        _hostedServices = hostedServices;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var backgroundServices = _hostedServices
            .OfType<BackgroundService>()
            .ToList();

        var data = new Dictionary<string, object>
        {
            ["ServiceCount"] = backgroundServices.Count
        };

        // In a real implementation, you'd track service state
        // This is a simplified example
        return Task.FromResult(HealthCheckResult.Healthy(
            description: $"{backgroundServices.Count} background services registered",
            data: data));
    }
}

// Better approach: Track service state
public interface IBackgroundServiceMonitor
{
    void ReportHealthy(string serviceName);
    void ReportUnhealthy(string serviceName, Exception? exception = null);
    bool IsHealthy(string serviceName);
    IReadOnlyDictionary<string, ServiceHealth> GetAllServiceHealth();
}

public record ServiceHealth(
    string Name,
    bool IsHealthy,
    DateTime LastHeartbeat,
    Exception? LastException);

public class BackgroundServiceMonitor : IBackgroundServiceMonitor
{
    private readonly ConcurrentDictionary<string, ServiceHealth> _services = new();

    public void ReportHealthy(string serviceName)
    {
        _services[serviceName] = new ServiceHealth(
            serviceName,
            true,
            DateTime.UtcNow,
            null);
    }

    public void ReportUnhealthy(string serviceName, Exception? exception = null)
    {
        _services[serviceName] = new ServiceHealth(
            serviceName,
            false,
            DateTime.UtcNow,
            exception);
    }

    public bool IsHealthy(string serviceName)
    {
        return _services.TryGetValue(serviceName, out var health)
            && health.IsHealthy
            && DateTime.UtcNow - health.LastHeartbeat < TimeSpan.FromMinutes(5);
    }

    public IReadOnlyDictionary<string, ServiceHealth> GetAllServiceHealth()
    {
        return _services;
    }
}

// Usage in background service
public class MonitoredBackgroundService : BackgroundService
{
    private readonly IBackgroundServiceMonitor _monitor;
    private readonly string _serviceName;

    public MonitoredBackgroundService(IBackgroundServiceMonitor monitor)
    {
        _monitor = monitor;
        _serviceName = GetType().Name;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
                _monitor.ReportHealthy(_serviceName);
            }
            catch (Exception ex)
            {
                _monitor.ReportUnhealthy(_serviceName, ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private Task DoWorkAsync(CancellationToken ct) => Task.CompletedTask;
}
```

---

## 10. Configuration-Driven Services

```csharp
// Configuration
// appsettings.json
{
  "BackgroundServices": {
    "Cleanup": {
      "Enabled": true,
      "IntervalMinutes": 60
    },
    "CacheWarmup": {
      "Enabled": true,
      "WarmupOnStartup": true
    },
    "QueueProcessor": {
      "Enabled": true,
      "WorkerCount": 3,
      "MaxQueueSize": 100
    }
  }
}

// Conditional registration
public static class BackgroundServiceExtensions
{
    public static IServiceCollection AddBackgroundServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        var settings = config.GetSection("BackgroundServices");

        if (settings.GetValue<bool>("Cleanup:Enabled"))
        {
            services.AddHostedService<TimedCleanupService>();
        }

        if (settings.GetValue<bool>("CacheWarmup:Enabled"))
        {
            services.AddHostedService<CacheWarmupService>();
        }

        if (settings.GetValue<bool>("QueueProcessor:Enabled"))
        {
            var queueSize = settings.GetValue<int>("QueueProcessor:MaxQueueSize", 100);
            services.AddSingleton<IBackgroundTaskQueue>(
                new BackgroundTaskQueue(queueSize));
            services.AddHostedService<ParallelQueueProcessor>();
        }

        return services;
    }
}

// Registration
builder.Services.AddBackgroundServices(builder.Configuration);
```

---

## 11. Testing Background Services

```csharp
public class BackgroundServiceTests
{
    [Fact]
    public async Task QueueProcessor_ProcessesWorkItems()
    {
        // Arrange
        var queue = new BackgroundTaskQueue();
        var processed = new List<int>();

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IBackgroundTaskQueue>(queue)
            .BuildServiceProvider();

        var processor = new QueuedHostedService(
            services.GetRequiredService<ILogger<QueuedHostedService>>(),
            queue,
            services);

        // Queue work items
        for (int i = 0; i < 5; i++)
        {
            var value = i;
            await queue.QueueAsync((_, _) =>
            {
                processed.Add(value);
                return ValueTask.CompletedTask;
            });
        }

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var task = processor.StartAsync(cts.Token);

        // Wait for processing
        while (processed.Count < 5 && !cts.IsCancellationRequested)
        {
            await Task.Delay(100);
        }

        cts.Cancel();
        await task;

        // Assert
        Assert.Equal(5, processed.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, processed.OrderBy(x => x));
    }

    [Fact]
    public async Task TimedService_ExecutesOnInterval()
    {
        // Arrange
        var executionCount = 0;

        var service = new TestTimedService(() =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await service.StartAsync(cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2.5));

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - should execute at least twice in 2.5 seconds with 1 second interval
        Assert.True(executionCount >= 2);
    }
}

// Test helper
public class TestTimedService : BackgroundService
{
    private readonly Func<Task> _action;

    public TestTimedService(Func<Task> action) => _action = action;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        await _action();

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _action();
        }
    }
}
```

---

## Summary

### Background Service Patterns

| Pattern | Use Case |
|---------|----------|
| Simple Loop | Continuous polling/processing |
| Timed/Scheduled | Periodic tasks (cleanup, reports) |
| Queue-Based | Async work from requests |
| Startup Task | One-time initialization |

### Best Practices

1. **Use scoped services correctly** — Create scope for each unit of work
2. **Handle exceptions** — Don't let unhandled exceptions crash the service
3. **Support graceful shutdown** — Respect cancellation tokens
4. **Monitor health** — Report service status to health checks
5. **Configure appropriately** — Make intervals and settings configurable
6. **Log meaningfully** — Track progress and errors

### Files to Create

```
src/
└── Shared/
    └── SharedKernel/
        └── BackgroundServices/
            ├── IBackgroundTaskQueue.cs
            ├── BackgroundTaskQueue.cs
            ├── QueuedHostedService.cs
            ├── TimedCleanupService.cs
            ├── CacheWarmupService.cs
            └── BackgroundServiceMonitor.cs
```
