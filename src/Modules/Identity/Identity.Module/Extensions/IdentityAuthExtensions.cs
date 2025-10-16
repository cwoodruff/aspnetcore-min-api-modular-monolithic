using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Identity.Modules.KeyManagement;
using Identity.Modules.Services;
using Identity.Modules.Authorization;

namespace Identity.Modules.Extensions;

public static class IdentityAuthExtensions
{
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
                };

                // Map inbound claims without remapping to legacy Microsoft claim types
                JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx => Task.CompletedTask,
                    OnTokenValidated = ctx => Task.CompletedTask,
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
