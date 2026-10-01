using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SharedKernel.Concurrency;

namespace SharedKernel.Diagnostics;

/// <summary>Per-module registrations for the in-process bulkheads (ADR-0013).</summary>
public static class ModuleBulkheadExtensions
{
    /// <summary>Registers the module's <see cref="ModuleMeter" />, keyed by module name. Safe to call more than once.</summary>
    public static IServiceCollection AddModuleMeter(this IServiceCollection services, string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        services.AddMetrics();
        services.TryAddKeyedSingleton(moduleName,
            (sp, _) => new ModuleMeter(sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>(), moduleName));
        return services;
    }

    /// <summary>
    /// Registers the module's bounded <see cref="ModuleWorkQueue" /> (keyed by module name, and started as a
    /// hosted service). Capacity: Concurrency:&lt;Module&gt;:QueueCapacity, default 64.
    /// </summary>
    public static IServiceCollection AddModuleWorkQueue(this IServiceCollection services, string moduleName)
    {
        services.AddModuleMeter(moduleName);
        services.AddKeyedSingleton(moduleName, (sp, _) =>
        {
            var capacity = sp.GetRequiredService<IConfiguration>().GetValue($"Concurrency:{moduleName}:QueueCapacity", 64);
            var queue = new ModuleWorkQueue(moduleName, capacity, sp.GetRequiredService<ILogger<ModuleWorkQueue>>());
            sp.GetRequiredKeyedService<ModuleMeter>(moduleName).ObserveQueueDepth(() => queue.Count);
            return queue;
        });
        services.AddHostedService(sp => sp.GetRequiredKeyedService<ModuleWorkQueue>(moduleName));
        return services;
    }

    /// <summary>
    /// Registers the module's <see cref="ModuleGate" />, keyed by module name. Limit:
    /// Concurrency:&lt;Module&gt;:MaxConcurrentExpensive, default 4.
    /// </summary>
    public static IServiceCollection AddModuleGate(this IServiceCollection services, string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        services.AddKeyedSingleton(moduleName, (sp, _) => new ModuleGate(moduleName,
            sp.GetRequiredService<IConfiguration>().GetValue($"Concurrency:{moduleName}:MaxConcurrentExpensive", 4)));
        return services;
    }

    /// <summary>
    /// Counts every response from the group's endpoints on the module's request counter, tagged with the
    /// final status code. Requests rejected before the endpoint runs (authentication, authorization, rate
    /// limiting) are not counted here; ASP.NET Core's own meters report those.
    /// </summary>
    public static TBuilder AddModuleMetrics<TBuilder>(this TBuilder builder, string moduleName)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var meter = http.RequestServices.GetRequiredKeyedService<ModuleMeter>(moduleName);
            http.Response.OnCompleted(() =>
            {
                meter.Request(http.Response.StatusCode);
                return Task.CompletedTask;
            });
            return await next(context);
        });
        return builder;
    }

    /// <summary>
    /// Adds a health check named after the module, tagged with the module name and "database", that opens
    /// the module's own <typeparamref name="TContext" />. The host reports it at /healthz.
    /// </summary>
    public static IServiceCollection AddModuleDbContextHealthCheck<TContext>(this IServiceCollection services,
        string moduleName)
        where TContext : DbContext
    {
        services.AddHealthChecks().Add(new HealthCheckRegistration(
            moduleName,
            sp => new ModuleDbContextHealthCheck<TContext>(sp.GetRequiredService<IServiceScopeFactory>()),
            HealthStatus.Unhealthy,
            [moduleName, "database"]));
        return services;
    }

    private sealed class ModuleDbContextHealthCheck<TContext>(IServiceScopeFactory scopes) : IHealthCheck
        where TContext : DbContext
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TContext>();
            try
            {
                return await db.Database.CanConnectAsync(ct)
                    ? HealthCheckResult.Healthy($"{typeof(TContext).Name} reachable")
                    : HealthCheckResult.Unhealthy($"{typeof(TContext).Name} cannot connect");
            }
#pragma warning disable CA1031 // A health probe reports failure; it does not throw.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                return HealthCheckResult.Unhealthy($"{typeof(TContext).Name} cannot connect", ex);
            }
        }
    }
}
