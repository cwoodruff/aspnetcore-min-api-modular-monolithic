namespace SharedKernel.TrafficControl;

/// <summary>
/// Rate-limit policy names. Each module applies its own policy once, on its route group (ADR-0013), so
/// a burst against one module exhausts that module's budget only. Limits come from configuration under
/// RateLimiting:Policies:&lt;name&gt; (PermitLimit, WindowSeconds, QueueLimit).
/// </summary>
public static class RateLimitPolicyRegistry
{
    public static class Names
    {
        /// <summary>The host's root endpoint only.</summary>
        public const string GlobalPublicAnon = "global:public-anon";

        public const string Catalog = "catalog:api";
        public const string Orders = "orders:api";
        public const string Administration = "admin:api";
        public const string Identity = "identity:api";
        public const string Reporting = "reporting:api";
    }
}
