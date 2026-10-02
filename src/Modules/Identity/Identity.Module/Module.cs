using Identity.Modules.Endpoints;
using Identity.Modules.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using SharedKernel.Diagnostics;
using SharedKernel.TrafficControl;
using SharedKernel.Validation;

namespace Identity.Modules;

public static class IdentityModule
{
    /// <summary>The module's name and its key for keyed services, caches, metrics and rate limits.</summary>
    internal const string ModuleName = "Identity";

    public sealed class Modules : IModule
    {
        public string Name => ModuleName;

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            services.AddModuleMeter(ModuleName);
            services.AddModuleRateLimitPolicy(RateLimitPolicyRegistry.Identity);

            // Request bodies are checked by ValidationFilter before the handlers run.
            services.AddSingleton<AuthRequestValidators>();
            services.AddSingleton<IRequestValidator<AuthHandlers.LoginRequest>>(sp => sp.GetRequiredService<AuthRequestValidators>());
            services.AddSingleton<IRequestValidator<AuthHandlers.RefreshRequest>>(sp => sp.GetRequiredService<AuthRequestValidators>());
            services.AddSingleton<IRequestValidator<AuthHandlers.LogoutRequest>>(sp => sp.GetRequiredService<AuthRequestValidators>());
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/identity")
                .RequireRateLimiting(RateLimitPolicyRegistry.Identity)
                .AddModuleMetrics(ModuleName);

            // Delegate to endpoint classes
            group.MapIdentityHealthEndpoints();
            group.MapIdentityDataHealthEndpoints();
            group.MapIdentityAuthEndpoints();
        }
    }
}
