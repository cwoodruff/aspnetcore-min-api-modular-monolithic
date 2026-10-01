namespace Identity.Contracts;

/// <summary>
/// Authorization policy names that are not permissions. Identity registers them; other modules apply
/// them by these constants (ADR-0011).
/// </summary>
public static class Policies
{
    /// <summary>Requires the Admin role.</summary>
    public const string Admin = "role.admin";

    /// <summary>Requires the X-Tenant-Id header to match the user's tenant claim.</summary>
    public const string TenantScoped = "tenant.scoped";
}
