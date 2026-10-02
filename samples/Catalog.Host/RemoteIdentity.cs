using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Identity.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Catalog.Host;

/// <summary>
///     Identity seen from outside the monolith: tokens are validated against the keys Identity publishes at
///     /api/identity/.well-known/jwks.json, and the policies Identity.Contracts names are registered here
///     with Identity's rules. Once Catalog leaves, these copies are what must track Identity
///     (ADR-0020, docs/extraction-playbook.md).
/// </summary>
internal static class RemoteIdentity
{
    public static IServiceCollection AddRemoteIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwksUrl = configuration["CatalogHost:IdentityJwksUrl"]
                      ?? throw new InvalidOperationException("CatalogHost:IdentityJwksUrl is not configured.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                jwksUrl, new JwksRetriever(), new HttpDocumentRetriever { RequireHttps = jwksUrl.StartsWith("https", StringComparison.Ordinal) });
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = configuration["Jwt:Issuer"] ?? "https://auth.local",
                ValidAudience = configuration["Jwt:Audience"] ?? "modular-api",
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role,
                ValidTypes = ["at+jwt", "JWT"]
            };
        });

        services.AddHttpContextAccessor();
        services.AddAuthorization(options =>
        {
            foreach (var permission in typeof(Permissions).GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!))
            {
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("permissions", permission));
            }

            options.AddPolicy(Policies.Admin, policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));

            // Identity's tenant rule: a tenant claim is required; an X-Tenant-Id header, if sent, must match it.
            options.AddPolicy(Policies.TenantScoped, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
            {
                var userTenant = context.User.FindFirstValue("tenant");
                if (string.IsNullOrWhiteSpace(userTenant))
                {
                    return false;
                }

                var requestTenant = (context.Resource as HttpContext)?.Request.Headers["X-Tenant-Id"].ToString();
                return string.IsNullOrWhiteSpace(requestTenant) || string.Equals(requestTenant, userTenant, StringComparison.Ordinal);
            }));
        });

        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        return services;
    }

    /// <summary>Identity publishes a key set, not an OpenID discovery document; this reads the key set alone.</summary>
    private sealed class JwksRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever,
            CancellationToken cancel)
        {
            var configuration = new OpenIdConnectConfiguration();
            foreach (var key in new JsonWebKeySet(await retriever.GetDocumentAsync(address, cancel)).GetSigningKeys())
            {
                configuration.SigningKeys.Add(key);
            }

            return configuration;
        }
    }
}
