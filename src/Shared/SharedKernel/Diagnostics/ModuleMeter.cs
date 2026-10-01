using System.Diagnostics.Metrics;

namespace SharedKernel.Diagnostics;

/// <summary>
/// One <see cref="Meter" /> per module, named "ModularMonolith.&lt;Module&gt;". Every measurement carries a
/// <c>module</c> tag, so any dashboard can split traffic, cache use and outbox flow by module (ADR-0013).
/// Resolved keyed by module name.
/// </summary>
public sealed class ModuleMeter : IDisposable
{
    public const string MeterNamePrefix = "ModularMonolith.";
    public const string Requests = "modmono.http.requests";
    public const string CacheHits = "modmono.cache.hits";
    public const string CacheMisses = "modmono.cache.misses";
    public const string OutboxPublished = "modmono.outbox.published";
    public const string OutboxDispatched = "modmono.outbox.dispatched";
    public const string OutboxRetried = "modmono.outbox.retried";
    public const string OutboxDeadLettered = "modmono.outbox.dead_lettered";
    public const string WorkQueueDepth = "modmono.work_queue.depth";

    private readonly Meter _meter;
    private readonly KeyValuePair<string, object?> _module;
    private readonly Counter<long> _requests;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _published;
    private readonly Counter<long> _dispatched;
    private readonly Counter<long> _retried;
    private readonly Counter<long> _deadLettered;

    public ModuleMeter(IMeterFactory meterFactory, string module)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        Module = module;
        _module = new KeyValuePair<string, object?>("module", module);
        _meter = meterFactory.Create(MeterNamePrefix + module);
        _requests = _meter.CreateCounter<long>(Requests, "{request}", "Requests handled by the module's endpoints, by status code.");
        _cacheHits = _meter.CreateCounter<long>(CacheHits, "{hit}", "Cache lookups served from the module's cache.");
        _cacheMisses = _meter.CreateCounter<long>(CacheMisses, "{miss}", "Cache lookups that had to load the value.");
        _published = _meter.CreateCounter<long>(OutboxPublished, "{event}", "Events added to the module's outbox (counted at publish, before the transaction commits).");
        _dispatched = _meter.CreateCounter<long>(OutboxDispatched, "{event}", "Outbox events delivered to every handler.");
        _retried = _meter.CreateCounter<long>(OutboxRetried, "{event}", "Failed deliveries scheduled for a retry.");
        _deadLettered = _meter.CreateCounter<long>(OutboxDeadLettered, "{event}", "Events dead-lettered after the last retry.");
    }

    public string Module { get; }

    public void Request(int statusCode) => _requests.Add(1, _module, new KeyValuePair<string, object?>("status_code", statusCode));

    public void CacheHit() => _cacheHits.Add(1, _module);

    public void CacheMiss() => _cacheMisses.Add(1, _module);

    public void Published() => _published.Add(1, _module);

    public void Dispatched() => _dispatched.Add(1, _module);

    public void Retried() => _retried.Add(1, _module);

    public void DeadLettered() => _deadLettered.Add(1, _module);

    /// <summary>Reports a work queue's depth as an observable gauge.</summary>
    public void ObserveQueueDepth(Func<int> depth) =>
        _meter.CreateObservableGauge(WorkQueueDepth, () => new Measurement<int>(depth(), _module), "{item}",
            "Work items waiting in the module's bounded queue.");

    public void Dispose() => _meter.Dispose();
}
