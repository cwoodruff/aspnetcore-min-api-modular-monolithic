using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.TrafficControl;

/// <summary>
/// Centralized registration and middleware hooks for rate limiting.
/// NOTE: This is scaffolding only. Implementation should bind policies from configuration
/// and register ASP.NET Core rate limiting with named policies. Modules must only reference
/// policy names and never embed limiter logic.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Registers the centralized rate limiting layer.
    /// Intended usage from the API host: builder.Services.AddRateLimiting(configuration);
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // Scaffold only: bind options and register policy registry.
        // In a future implementation, this will:
        // - Read RateLimiting:* configuration
        // - Register named policies (AddRateLimiter)
        // - Configure standard 429 header writer
        services.AddSingleton<RateLimitPolicyRegistry>();
        return services;
    }

    /// <summary>
    /// Adds the rate limiting middleware to the pipeline.
    /// Intended usage from the API host: app.UseRateLimiting();
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        // Scaffold only: in implementation, call app.UseRateLimiter();
        return app;
    }
}
