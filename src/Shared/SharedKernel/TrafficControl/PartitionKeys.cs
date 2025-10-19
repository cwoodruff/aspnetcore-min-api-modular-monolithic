using Microsoft.AspNetCore.Http;

namespace SharedKernel.TrafficControl;

/// <summary>
/// Helpers to derive a stable partition key for rate limiting from the current request.
/// This is scaffolding only; the final implementation should align with ForwardedHeaders config.
/// </summary>
public static class PartitionKeys
{
    public static string FromRequest(HttpContext httpContext)
    {
        // Priority: API key/client_id -> tenant -> sub -> IP
        var clientId = httpContext.User.FindFirst("client_id")?.Value;
        if (!string.IsNullOrWhiteSpace(clientId)) return $"client:{Normalize(clientId)}";

        var tenant = httpContext.User.FindFirst("tenant")?.Value;
        if (!string.IsNullOrWhiteSpace(tenant)) return $"tenant:{Normalize(tenant)}";

        var sub = httpContext.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(sub)) return $"sub:{Normalize(sub)}";

        var ip = GetClientIp(httpContext);
        return $"ip:{ip}";
    }

    private static string GetClientIp(HttpContext context)
    {
        // Minimal, conservative approach: use RemoteIpAddress. Forwarded headers trust should be configured at host.
        var ip = context.Connection.RemoteIpAddress;
        return ip is null ? "unknown" : ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? ip.MapToIPv4().ToString()
            : ip.ToString();
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
