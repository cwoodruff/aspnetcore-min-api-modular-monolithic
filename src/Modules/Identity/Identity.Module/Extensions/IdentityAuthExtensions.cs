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

        // Key material service (dev default; swappable via config later)
        services.AddSingleton<IKeyMaterialService, DevKeyMaterialService>();

        // Core services
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
        services.AddSingleton<IUserStore, InMemoryUserStore>();

        // HttpContext + tenant resolution + authorization handlers
        services.AddHttpContextAccessor();
        services.AddSingleton<ITenantResolutionService, HttpContextTenantResolutionService>();
        services.AddSingleton<IAuthorizationHandler, TenantAuthorizationHandler>();

        // Authorization policies (by permissions)
        services.AddAuthorization(options =>
        {
            PolicyRegistry.Register(options);
        });

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

                options.IncludeErrorDetails = true;
                options.RequireHttpsMetadata = false; // enable in production behind TLS
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOpts.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOpts.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
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
                        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
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

                        var sub = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
                    OnChallenge = ctx => Task.CompletedTask,
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

public sealed class JwtAuthOptions
{
    public string Issuer { get; set; } = "https://auth.local";
    public string Audience { get; set; } = "modular-api";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
    public string KeyProvider { get; set; } = "Dev"; // Dev|KeyVault (future)
    public string? KeyVaultVaultUri { get; set; }
    public string? KeyVaultKeyName { get; set; }
}
