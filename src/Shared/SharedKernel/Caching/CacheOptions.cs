using Microsoft.Extensions.Options;

namespace SharedKernel.Caching;

public sealed class CacheOptions
{
    public bool Enabled { get; set; } = true;
    public string Tier { get; set; } = "L1"; // L1 | L1L2
    public string Provider { get; set; } = "InMemory"; // InMemory | Redis | SqlServer | NCache (future)

    // Global defaults (overridable per module via PerModule)
    public int DefaultTTLSeconds { get; set; } = 300;

    public SwrOptions SWR { get; set; } = new();
    public StampedeOptions Stampede { get; set; } = new();
    public PartitioningOptions Partitioning { get; set; } = new();

    // Per-module default TTLs (optional)
    public ModuleTtls PerModule { get; set; } = new();

    // Redis provider options (if used)
    public RedisOptions Redis { get; set; } = new();
}

public sealed class SwrOptions
{
    public bool Enabled { get; set; }
    public int MaxStaleSeconds { get; set; } // 0 disables
}

public sealed class StampedeOptions
{
    public bool SingleFlight { get; set; } = true;
}

public sealed class PartitioningOptions
{
    public bool TenantAware { get; set; }
    public bool RegionAware { get; set; }
}

public sealed class ModuleTtls
{
    public int? Music { get; set; }
    public int? Orders { get; set; }
    public int? Administration { get; set; }
    public int? Reporting { get; set; }
    public int? Identity { get; set; }
}

public sealed class RedisOptions
{
    public string? ConnectionString { get; set; }
    public string? InstanceName { get; set; }
    public bool SSL { get; set; } = true;
    public int PoolSize { get; set; } = 10;
}
