using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.TrafficControl;

public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds a fixed-window policy named <paramref name="policyName" />, partitioned by
    /// <see cref="PartitionKeys.FromRequest" />. Limits are read from RateLimiting:Policies:&lt;name&gt; and
    /// default to 60 requests per 60 seconds with no queue.
    /// </summary>
    public static IServiceCollection AddModuleRateLimitPolicy(this IServiceCollection services, string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            var section = configuration.GetSection($"RateLimiting:Policies:{policyName}");
            var permitLimit = section.GetValue("PermitLimit", 60);
            var window = TimeSpan.FromSeconds(section.GetValue("WindowSeconds", 60.0));
            var queueLimit = section.GetValue("QueueLimit", 0);

            options.AddPolicy(policyName, context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKeys.FromRequest(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = queueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                }));
        });
        return services;
    }
}
