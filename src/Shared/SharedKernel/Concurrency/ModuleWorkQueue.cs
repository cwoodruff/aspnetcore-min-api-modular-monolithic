using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Concurrency;

/// <summary>
/// A module's bounded background queue (ADR-0013): a <see cref="Channel{T}" /> with
/// <see cref="BoundedChannelFullMode.Wait" /> and one consumer. When the module's background work falls
/// behind, producers wait instead of piling up work in memory, and the module's work never runs on more
/// than one thread at a time. Resolved keyed by module name; its depth is a metric.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711",
    Justification = "It is a queue; the name is the one the upgrade plan and ADR-0013 use.")]
public sealed partial class ModuleWorkQueue : BackgroundService
{
    private readonly Channel<Func<CancellationToken, Task>> _channel;
    private readonly ILogger<ModuleWorkQueue> _logger;

    public ModuleWorkQueue(string module, int capacity, ILogger<ModuleWorkQueue> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Module = module;
        Capacity = capacity;
        _logger = logger;
        _channel = Channel.CreateBounded<Func<CancellationToken, Task>>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    public string Module { get; }

    public int Capacity { get; }

    /// <summary>Items waiting, not counting the one running.</summary>
    public int Count => _channel.Reader.Count;

    /// <summary>Queues <paramref name="work" />; waits while the queue is full.</summary>
    public ValueTask EnqueueAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        return _channel.Writer.WriteAsync(work, ct);
    }

    /// <summary>Queues <paramref name="work" /> and waits for its result.</summary>
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        await EnqueueAsync(async token =>
        {
            try
            {
                completion.TrySetResult(await work(token));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                completion.TrySetCanceled(token);
            }
#pragma warning disable CA1031 // The failure belongs to the caller awaiting the result.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                completion.TrySetException(ex);
            }
        }, ct);
        return await completion.Task.WaitAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var work in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await work(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
#pragma warning disable CA1031 // One failed item must not stop the module's queue.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    WorkFailed(_logger, Module, ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Work item failed in the {Module} work queue")]
    private static partial void WorkFailed(ILogger logger, string module, Exception exception);
}
