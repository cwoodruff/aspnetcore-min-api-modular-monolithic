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
using Microsoft.Extensions.Logging;
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
        services.Configure<InMemoryUserStoreOptions>(configuration.GetSection("Identity"));
        services.AddSingleton<IUserStore>(sp =>
        {
            var environment = sp.GetRequiredService<IHostEnvironment>();
            return environment.IsDevelopment() || environment.IsEnvironment("Demo")
                ? ActivatorUtilities.CreateInstance<InMemoryUserStore>(sp)
                : new DisabledUserStore();
        });
        services.AddHostedService<InMemoryUserStoreDiagnosticsHostedService>();

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

internal sealed partial class InMemoryUserStoreDiagnosticsHostedService(
    IHostEnvironment environment,
    IOptions<InMemoryUserStoreOptions> options,
    ILogger<InMemoryUserStoreDiagnosticsHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var configuredUsers = options.Value.InMemoryUsers;
        var validUsers = configuredUsers
            .Select((user, index) => new
            {
                Index = index,
                User = user,
                MissingRequiredFields = GetMissingRequiredFields(user)
            })
            .Where(entry => entry.MissingRequiredFields.Length == 0)
            .ToArray();
        var configuredCount = configuredUsers.Count;
        var invalidUsers = configuredUsers
            .Select((user, index) => new
            {
                Index = index,
                MissingRequiredFields = GetMissingRequiredFields(user)
            })
            .Where(entry => entry.MissingRequiredFields.Length > 0)
            .ToArray();
        var validCount = validUsers.Length;
        var ignoredCount = invalidUsers.Length;
        var isDevelopmentOrDemo = environment.IsDevelopment() || environment.IsEnvironment("Demo");

        if (!isDevelopmentOrDemo)
        {
            if (configuredCount > 0)
            {
                LogInMemoryUsersDisabledOutsideDevelopment(logger, configuredCount, environment.EnvironmentName);
            }

            return Task.CompletedTask;
        }

        // Report each incomplete entry before the no-valid-users early return, so a first-time setup
        // where every entry is incomplete still names the index and the missing field(s).
        foreach (var invalidUser in invalidUsers)
        {
            LogIncompleteInMemoryUserEntry(
                logger,
                invalidUser.Index,
                string.Join(", ", invalidUser.MissingRequiredFields));
        }

        if (validCount == 0)
        {
            if (configuredCount == 0)
            {
                LogNoInMemoryUsersConfigured(logger, environment.EnvironmentName);
            }
            else
            {
                LogNoValidInMemoryUsersConfigured(logger, environment.EnvironmentName, configuredCount);
            }

            return Task.CompletedTask;
        }

        if (ignoredCount > 0)
        {
            LogIgnoredIncompleteInMemoryUsers(logger, validCount, ignoredCount);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            foreach (var validUser in validUsers)
            {
#pragma warning disable CA1873
                LogLoadedInMemoryUser(
                    logger,
                    validUser.Index,
                    validUser.User.Username.Trim(),
                    validUser.User.UserId.Trim(),
                    FormatValues(validUser.User.Roles),
                    FormatValues(validUser.User.Permissions),
                    string.IsNullOrWhiteSpace(validUser.User.Tenant) ? "(none)" : validUser.User.Tenant.Trim(),
                    validUser.User.Password.Length);
#pragma warning restore CA1873
            }
        }

        LogLoadedInMemoryUsers(logger, validCount, environment.EnvironmentName);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static string FormatValues(IEnumerable<string> values)
    {
        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

        return normalized.Length == 0 ? "(none)" : string.Join(", ", normalized);
    }

    private static string[] GetMissingRequiredFields(InMemoryUserRecord user)
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(user.Username))
        {
            missingFields.Add(nameof(InMemoryUserRecord.Username));
        }

        if (string.IsNullOrWhiteSpace(user.Password))
        {
            missingFields.Add(nameof(InMemoryUserRecord.Password));
        }

        if (string.IsNullOrWhiteSpace(user.UserId))
        {
            missingFields.Add(nameof(InMemoryUserRecord.UserId));
        }

        return missingFields.ToArray();
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message =
            "Identity:InMemoryUsers is configured with {ConfiguredCount} entries, but in-memory login is disabled in the {EnvironmentName} environment. Set ASPNETCORE_ENVIRONMENT=Development or Demo to enable it.")]
    private static partial void LogInMemoryUsersDisabledOutsideDevelopment(
        ILogger logger,
        int configuredCount,
        string environmentName);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message =
            "In-memory login is enabled for the {EnvironmentName} environment, but no Identity:InMemoryUsers entries are configured.")]
    private static partial void LogNoInMemoryUsersConfigured(ILogger logger, string environmentName);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message =
            "In-memory login is enabled for the {EnvironmentName} environment, but none of the {ConfiguredCount} configured Identity:InMemoryUsers entries are usable. Each entry must include Username, Password, and UserId.")]
    private static partial void LogNoValidInMemoryUsersConfigured(
        ILogger logger,
        string environmentName,
        int configuredCount);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Warning,
        Message =
            "Loaded {ValidCount} in-memory login users and ignored {IgnoredCount} incomplete Identity:InMemoryUsers entries. Each entry must include Username, Password, and UserId.")]
    private static partial void LogIgnoredIncompleteInMemoryUsers(ILogger logger, int validCount, int ignoredCount);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Warning,
        Message =
            "Ignoring Identity:InMemoryUsers:{Index} because it is missing required field(s): {MissingRequiredFields}. Keep Username, Password, UserId, Roles, Permissions, Email, and Tenant for one account on the same array index.")]
    private static partial void LogIncompleteInMemoryUserEntry(
        ILogger logger,
        int index,
        string missingRequiredFields);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Loaded {ValidCount} in-memory login user(s) for the {EnvironmentName} environment.")]
    private static partial void LogLoadedInMemoryUsers(ILogger logger, int validCount, string environmentName);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message =
            "Effective Identity:InMemoryUsers:{Index} => Username='{Username}', UserId='{UserId}', Roles='{Roles}', Permissions='{Permissions}', Tenant='{Tenant}', PasswordLength={PasswordLength}.")]
    private static partial void LogLoadedInMemoryUser(
        ILogger logger,
        int index,
        string username,
        string userId,
        string roles,
        string permissions,
        string tenant,
        int passwordLength);
}
