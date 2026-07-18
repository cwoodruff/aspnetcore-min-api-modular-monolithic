using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Identity.Modules.Authorization;
using Identity.Modules.KeyManagement;
using Identity.Modules.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Modules.Extensions;

public static class IdentityAuthExtensions
{
    private static readonly string[] ValidTokenTypes = new[] { "at+jwt", "JWT" };

    public static IServiceCollection AddIdentityAuth(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<JwtAuthOptions>(configuration.GetSection("Jwt"));

        // Key material service
        services.AddSingleton<IKeyMaterialService>(sp =>
        {
            var environment = sp.GetRequiredService<IHostEnvironment>();
            var jwtOptions = sp.GetRequiredService<IOptions<JwtAuthOptions>>().Value;
            var provider = jwtOptions.KeyProvider?.Trim();

            if (string.Equals(provider, "KeyVault", StringComparison.OrdinalIgnoreCase))
            {
                return ActivatorUtilities.CreateInstance<KeyVaultKeyMaterialService>(sp);
            }

            if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "Dev", StringComparison.OrdinalIgnoreCase))
            {
                if (environment.IsDevelopment() || environment.IsEnvironment("Demo"))
                {
                    return ActivatorUtilities.CreateInstance<DevKeyMaterialService>(sp);
                }

                throw new InvalidOperationException(
                    "JWT signing keys must use an external provider outside the Development or Demo environment. " +
                    "Set Jwt:KeyProvider=KeyVault and configure Jwt:KeyVaultVaultUri plus Jwt:KeyVaultKeyName.");
            }

            throw new InvalidOperationException(
                $"Unsupported Jwt:KeyProvider value '{provider}'. Supported values are 'Dev' and 'KeyVault'.");
        });

        // Core services
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
        services.Configure<InMemoryUserStoreOptions>(configuration.GetSection("Identity:InMemoryUsers"));
        services.AddSingleton<IUserStore>(sp =>
        {
            var environment = sp.GetRequiredService<IHostEnvironment>();
            return environment.IsDevelopment() || environment.IsEnvironment("Demo")
                ? ActivatorUtilities.CreateInstance<InMemoryUserStore>(sp)
                : new DisabledUserStore();
        });

        // HttpContext + tenant resolution + authorization handlers
        services.AddHttpContextAccessor();
        services.AddSingleton<ITenantResolutionService, HttpContextTenantResolutionService>();
        services.AddSingleton<IAuthorizationHandler, TenantAuthorizationHandler>();

        // Authorization policies (by permissions)
        services.AddAuthorization(options => { PolicyRegistry.Register(options); });

        // Authentication: JWT Bearer
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IKeyMaterialService, IOptions<JwtAuthOptions>>((options, keys, jwtOptsAccessor) =>
            {
                var jwtOpts = jwtOptsAccessor.Value;

                // OWASP A05: Do not expose JWT validation error details to clients
                options.IncludeErrorDetails = false;
                // OWASP A02: Require HTTPS for token metadata in non-development environments
                options.RequireHttpsMetadata = !string.Equals(
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    "Development", StringComparison.OrdinalIgnoreCase);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOpts.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOpts.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = keys.GetValidationKeys(),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                    ValidTypes = ValidTokenTypes
                };

                // Map inbound claims without remapping to legacy Microsoft claim types
                JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        // Normalize Authorization header in case Swagger/UI or clients send 'Bearer Bearer <token>'
                        // We tolerate a duplicated scheme prefix by trimming one extra occurrence.
                        var authHeader = ctx.Request.Headers.Authorization.ToString();
                        if (!string.IsNullOrWhiteSpace(authHeader) &&
                            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            var token = authHeader.Substring("Bearer ".Length).Trim();
                            if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                            {
                                token = token.Substring("Bearer ".Length).Trim();
                            }

                            ctx.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = ctx =>
                    {
                        var principal = ctx.Principal;
                        if (principal is null)
                        {
                            ctx.Fail("No principal after token validation");
                            return Task.CompletedTask;
                        }

                        var sub = principal.FindFirst("sub")?.Value ??
                                  principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (string.IsNullOrWhiteSpace(sub))
                        {
                            ctx.Fail("Missing required 'sub' claim");
                            return Task.CompletedTask;
                        }

                        // Tenant claim recommended for multi-tenant modules; do not fail if absent to allow public endpoints
                        // var tenant = principal.FindFirst("tenant")?.Value; // informational for handlers
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = ctx => Task.CompletedTask,
                    OnChallenge = ctx => Task.CompletedTask
                };
            });

        return services;
    }
    public static IApplicationBuilder UseIdentityAuth(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}

internal sealed class JwtAuthOptions
{
    public string Issuer { get; set; } = "https://auth.local";
    public string Audience { get; set; } = "modular-api";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
    public string KeyProvider { get; set; } = "Dev";
    public string DevelopmentKeyPath { get; set; } = "data/identity/dev-jwt-signing-key.json";
    public string? KeyVaultVaultUri { get; set; }
    public string? KeyVaultKeyName { get; set; }
}
