namespace SharedKernel.Concurrency;

/// <summary>
/// Caps how many expensive operations of one module run at once (ADR-0013), so a burst of, say, report
/// queries cannot take every database connection and thread from the other modules. Resolved keyed by
/// module name.
/// </summary>
public sealed class ModuleGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public ModuleGate(string module, int maxConcurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);
        Module = module;
        MaxConcurrency = maxConcurrency;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public string Module { get; }

    public int MaxConcurrency { get; }

    public int Available => _semaphore.CurrentCount;

    /// <summary>Waits for a slot; dispose the returned lease to release it.</summary>
    public async ValueTask<IDisposable> EnterAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        return new Lease(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
