using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Concurrency;

namespace ModularMonolith.Services.Tests.Bulkheads;

public sealed class ConcurrencyPrimitiveTests
{
    [Fact]
    public async Task WorkQueue_RunsWorkOneAtATime_AndProducersWaitWhenItIsFull()
    {
        using var queue = new ModuleWorkQueue("Test", capacity: 1, NullLogger<ModuleWorkQueue>.Instance);
        await queue.StartAsync(CancellationToken.None);

        var release = new TaskCompletionSource();
        var running = 0;
        var maxRunning = 0;
        async Task<int> Work(CancellationToken ct)
        {
            var now = Interlocked.Increment(ref running);
            maxRunning = Math.Max(maxRunning, now);
            await release.Task.WaitAsync(ct);
            Interlocked.Decrement(ref running);
            return now;
        }

        var first = queue.RunAsync(Work, CancellationToken.None); // taken by the consumer
        await Task.Delay(50);
        var second = queue.RunAsync(Work, CancellationToken.None); // fills the one slot
        var third = queue.RunAsync(Work, CancellationToken.None); // must wait for room
        await Task.Delay(50);
        queue.Count.Should().Be(1, "capacity is 1; the third producer is waiting, not queued");

        release.SetResult();
        await Task.WhenAll(first, second, third);
        maxRunning.Should().Be(1);
        await queue.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Gate_AdmitsAtMostItsLimit()
    {
        using var gate = new ModuleGate("Test", maxConcurrency: 2);

        var a = await gate.EnterAsync(CancellationToken.None);
        var b = await gate.EnterAsync(CancellationToken.None);
        var c = gate.EnterAsync(CancellationToken.None).AsTask();
        await Task.Delay(50);
        c.IsCompleted.Should().BeFalse("two leases are held");

        a.Dispose();
        (await c.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
        b.Dispose();
        gate.Available.Should().Be(2);
    }
}
