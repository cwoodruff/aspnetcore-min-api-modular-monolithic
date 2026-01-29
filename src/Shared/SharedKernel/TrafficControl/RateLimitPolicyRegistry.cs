using Microsoft.Extensions.Configuration;

namespace SharedKernel.TrafficControl;

/// <summary>
///     Central registry for rate limiting policy names and configuration binding.
///     This is scaffolding only: populate constants for common policy names and
///     later bind thresholds/algorithms from configuration under RateLimiting:Policies.
/// </summary>
public sealed class RateLimitPolicyRegistry(IConfiguration configuration)
{
    /// <summary>
    ///     Placeholder for future binding of policies from configuration.
    /// </summary>
    public IConfiguration Section => configuration.GetSection("RateLimiting");

    // Canonical policy names (modules should reference these names only)
    public static class Names
    {
        public const string GlobalPublicAnon = "global:public-anon";
        public const string GlobalUserStandard = "global:user-standard";
        public const string GlobalTenantStandard = "global:tenant-standard";
        public const string GlobalAdminElevated = "global:admin-elevated";
        public const string ReportingHeavy = "reporting:heavy";
    }
}
