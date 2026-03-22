using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Modules.Extensions;
using Identity.Modules.KeyManagement;
using Microsoft.Extensions.Options;

namespace Identity.Modules.Services;

public sealed class TokenService(
    IOptions<JwtAuthOptions> options,
    IKeyMaterialService keys,
    IRefreshTokenStore refreshStore,
    IUserStore userStore) : ITokenService
{
    private readonly JwtAuthOptions _opts = options.Value;

    public async Task<TokenPair> IssueAsync(string userId, string? displayName, string[] roles, string[] permissions,
        string? email, string? tenant, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_opts.AccessTokenMinutes);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Exp, expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Iss, _opts.Issuer),
            new(JwtRegisteredClaimNames.Aud, _opts.Audience)
        };

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, displayName));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (!string.IsNullOrWhiteSpace(tenant))
        {
            claims.Add(new Claim("tenant", tenant));
        }

        foreach (var role in roles ?? [])
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var p in permissions ?? [])
        {
            claims.Add(new Claim("permissions", p));
        }

        var credentials = keys.GetCurrentSigningCredentials();
        var token = new JwtSecurityToken(
            _opts.Issuer,
            _opts.Audience,
            claims,
            now.UtcDateTime,
            expires.UtcDateTime,
            credentials
        );
        token.Header[JwtHeaderParameterNames.Kid] = keys.GetCurrentKeyId();

        var handler = new JwtSecurityTokenHandler();
        var access = handler.WriteToken(token);

        // Generate refresh token (opaque)
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        await refreshStore.StoreAsync(userId, refreshToken, now.AddDays(_opts.RefreshTokenDays), null, ct);

        return new TokenPair(access, refreshToken, expires);
    }

    public async Task<TokenPair?> RefreshAsync(string userId, string refreshToken, CancellationToken ct = default)
    {
        var valid = await refreshStore.ValidateAsync(userId, refreshToken, ct);
        if (!valid)
        {
            return null;
        }

        // OWASP A01: Re-fetch current user permissions/tenant on refresh
        // to ensure revoked permissions are not carried over in new tokens
        var user = await userStore.GetUserByIdAsync(userId, ct);
        if (!user.found)
        {
            // User may have been deactivated since the refresh token was issued
            await refreshStore.RevokeAsync(userId, refreshToken, ct);
            return null;
        }

        var pair = await IssueAsync(userId, user.displayName, user.roles, user.permissions, user.email, user.tenant, ct);

        // Revoke old refresh token (rotation)
        await refreshStore.RevokeAsync(userId, refreshToken, ct);
        return pair;
    }
}
