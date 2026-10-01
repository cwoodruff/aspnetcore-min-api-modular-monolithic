namespace SharedKernel.Caching;

/// <summary>Bound from the "Caching" configuration section; shared by every module's cache.</summary>
public sealed class CacheOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>"L1" (each module's own memory cache) or "L1L2" (adds the shared distributed cache).</summary>
    public string Tier { get; set; } = "L1";

    /// <summary>The L2 provider when Tier is L1L2; "InMemory" registers a distributed memory cache.</summary>
    public string Provider { get; set; } = "InMemory";

    /// <summary>Time to live when an entry does not set one.</summary>
    public int DefaultTTLSeconds { get; set; } = 300;
}
