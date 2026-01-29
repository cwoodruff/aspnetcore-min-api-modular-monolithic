# Session 4: Authentication & Authorization

**Duration:** 75 minutes
**Session Time:** 10:45 AM - 12:00 PM

---

## Overview

This session covers JWT Bearer authentication, token generation with RS256
signing, policy-based authorization, and multi-tenant access control. You'll
understand the complete Identity module implementation.

---

## Learning Objectives

By the end of this session, you will:

- Understand JWT token structure and RS256 signing
- Implement token generation and validation
- Create permission-based authorization policies
- Implement tenant-scoped authorization
- Protect endpoints with RequireAuthorization
- Know how to test authenticated endpoints

---

## Part 1: JWT Architecture (15 minutes)

### 1.1 Token Structure

JWTs consist of three parts (Header.Payload.Signature):

**Header:**

```json
{
  "alg": "RS256",
  "typ": "at+jwt",
  "kid": "abc12345"
}
```

**Payload (Claims):**

```json
{
  "sub": "user-1",
  "name": "Demo User",
  "email": "demo@example.com",
  "roles": ["User"],
  "permissions": ["music.read", "orders.read"],
  "tenant": "tenant-123",
  "iat": 1234567890,
  "exp": 1234568790,
  "iss": "https://auth.local",
  "aud": "modular-api"
}
```

### 1.2 Authentication Flow

```
1. Client POSTs credentials to /api/identity/login
2. Server validates credentials
3. Server generates JWT with user claims
4. Server returns access_token + refresh_token
5. Client includes token: Authorization: Bearer <token>
6. Server validates token signature and claims
7. Authorization policies evaluate claims
```

---

## Part 2: Identity Module Implementation (30 minutes)

### 2.1 JWT Options Configuration

**File:
`src/Modules/Identity/Identity.Module/Extensions/IdentityAuthExtensions.cs` (
partial)**

```csharp
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
```

### 2.2 Token Service Implementation

**File: `src/Modules/Identity/Identity.Module/Services/TokenService.cs`**

```csharp
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Modules.KeyManagement;
using Identity.Modules.Extensions;
using Microsoft.Extensions.Options;

namespace Identity.Modules.Services;

public sealed class TokenService(IOptions<JwtAuthOptions> options, IKeyMaterialService keys, IRefreshTokenStore refreshStore) : ITokenService
{
    private readonly JwtAuthOptions _opts = options.Value;

    public async Task<TokenPair> IssueAsync(string userId, string? displayName, string[] roles, string[] permissions, string? email, string? tenant, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_opts.AccessTokenMinutes);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Exp, expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Iss, _opts.Issuer),
            new(JwtRegisteredClaimNames.Aud, _opts.Audience),
        };

        if (!string.IsNullOrWhiteSpace(displayName)) claims.Add(new(ClaimTypes.Name, displayName));
        if (!string.IsNullOrWhiteSpace(email)) claims.Add(new(ClaimTypes.Email, email));
        if (!string.IsNullOrWhiteSpace(tenant)) claims.Add(new("tenant", tenant));
        foreach (var role in roles ?? []) claims.Add(new(ClaimTypes.Role, role));
        foreach (var p in permissions ?? []) claims.Add(new("permissions", p));

        var credentials = keys.GetCurrentSigningCredentials();
        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials
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
        if (!valid) return null;

        // For demo, static roles/permissions
        var pair = await IssueAsync(userId, displayName: userId, roles: Array.Empty<string>(), permissions: Array.Empty<string>(), email: null, tenant: null, ct);

        // Revoke old
        await refreshStore.RevokeAsync(userId, refreshToken, ct);
        return pair;
    }
}
```

### 2.3 Authentication Endpoints

**File: `src/Modules/Identity/Identity.Module/Endpoints/AuthEndpoints.cs`**

```csharp
using System.Security.Claims;
using Identity.Modules.KeyManagement;
using Identity.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Modules.Endpoints;

public static class AuthEndpoints
{
    public static void MapIdentityAuthEndpoints(this IEndpointRouteBuilder group)
    {
        // POST /api/identity/login
        group.MapPost("/login", async (LoginRequest req, IUserStore users, ITokenService tokens, CancellationToken ct) =>
        {
            var result = await users.ValidateCredentialsAsync(req.username, req.password, ct);
            if (!result.success)
            {
                return Results.Unauthorized();
            }

            var pair = await tokens.IssueAsync(result.userId, result.displayName, result.roles, result.permissions, result.email, result.tenant, ct);
            return Results.Json(new
            {
                access_token = pair.AccessToken,
                token_type = "Bearer",
                expires_at_utc = pair.ExpiresAtUtc,
                refresh_token = pair.RefreshToken,
            });
        })
        .AllowAnonymous()
        .WithTags("Identity")
        .WithName("IdentityLogin")
        .Produces(429)
        .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // POST /api/identity/refresh
        group.MapPost("/refresh", async (RefreshRequest req, ITokenService tokens, CancellationToken ct) =>
        {
            var pair = await tokens.RefreshAsync(req.userId, req.refreshToken, ct);
            if (pair is null) return Results.Unauthorized();
            return Results.Json(new
            {
                access_token = pair.AccessToken,
                token_type = "Bearer",
                expires_at_utc = pair.ExpiresAtUtc,
                refresh_token = pair.RefreshToken,
            });
        })
        .AllowAnonymous()
        .WithTags("Identity")
        .WithName("IdentityRefresh")
        .Produces(429)
        .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // POST /api/identity/logout
        group.MapPost("/logout", async (LogoutRequest req, IRefreshTokenStore store, CancellationToken ct) =>
        {
            await store.RevokeAsync(req.userId, req.refreshToken, ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithTags("Identity")
        .WithName("IdentityLogout")
        .Produces(429)
        .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/identity/userinfo
        group.MapGet("/userinfo", [Authorize] (ClaimsPrincipal user) =>
        {
            var response = new
            {
                sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier),
                name = user.FindFirstValue(ClaimTypes.Name),
                email = user.FindFirstValue(ClaimTypes.Email),
                roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
                permissions = user.FindAll("permissions").Select(c => c.Value).ToArray(),
            };
            return Results.Json(response);
        })
        .WithTags("Identity")
        .WithName("IdentityUserInfo");

        // GET /.well-known/jwks.json
        group.MapGet("/.well-known/jwks.json", (IKeyMaterialService keys) => Results.Json(keys.GetJwksDocument()))
            .AllowAnonymous()
            .WithTags("Identity")
            .WithName("IdentityJWKS")
            .Produces(429)
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }

    private sealed record LoginRequest(string username, string password);

    private sealed record RefreshRequest(string userId, string refreshToken);

    private sealed record LogoutRequest(string userId, string refreshToken);
}
```

---

## Part 3: Authorization Policies (15 minutes)

### 3.1 Permission Constants

**File: `src/Modules/Identity/Identity.Module/Authorization/Permissions.cs`**

```csharp
namespace Identity.Modules.Authorization;

public static class Permissions
{
    // Music
    public const string MusicRead = "music.read";
    public const string MusicWrite = "music.write";

    // Orders
    public const string OrdersRead = "orders.read";
    public const string OrdersWrite = "orders.write";

    // Administration
    public const string AdminUsersManage = "admin.users.manage";
    public const string AdministrationRead = "administration.read";
    public const string AdministrationWrite = "administration.write";

    // Reporting
    public const string ReportView = "report.view";
}
```

### 3.2 Policy Registry

**File: `src/Modules/Identity/Identity.Module/Authorization/PolicyRegistry.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;

namespace Identity.Modules.Authorization;

public static class PolicyRegistry
{
    public const string AdminPolicy = "role.admin";
    public const string TenantScopedPolicy = "tenant.scoped";

    public static void Register(AuthorizationOptions options)
    {
        // Permission-based policies
        AddPermissionPolicy(options, Permissions.MusicRead);
        AddPermissionPolicy(options, Permissions.MusicWrite);
        AddPermissionPolicy(options, Permissions.OrdersRead);
        AddPermissionPolicy(options, Permissions.OrdersWrite);
        AddPermissionPolicy(options, Permissions.AdminUsersManage);
        AddPermissionPolicy(options, Permissions.AdministrationRead);
        AddPermissionPolicy(options, Permissions.AdministrationWrite);
        AddPermissionPolicy(options, Permissions.ReportView);

        // Role-based convenience policies
        options.AddPolicy(AdminPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole("Admin");
        });

        // Tenant scoped policy
        options.AddPolicy(TenantScopedPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(TenantRequirement.Instance);
        });
    }

    private static void AddPermissionPolicy(AuthorizationOptions options, string permission)
    {
        options.AddPolicy(permission, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("permissions", permission);
        });
    }
}
```

### 3.3 Tenant Authorization Handler

**File:
`src/Modules/Identity/Identity.Module/Authorization/TenantAuthorizationHandler.cs`
**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Identity.Modules.Authorization;

public sealed class TenantAuthorizationHandler(
    ITenantResolutionService resolver,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<TenantRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantRequirement requirement)
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null)
        {
            return Task.CompletedTask;
        }

        var userTenant = context.User.FindFirstValue("tenant");
        if (string.IsNullOrWhiteSpace(userTenant))
        {
            // No tenant claim -> cannot satisfy tenant scoped resources
            return Task.CompletedTask;
        }

        var requestTenant = resolver.ResolveTenantId(http);
        if (string.IsNullOrWhiteSpace(requestTenant))
        {
            // If request does not specify tenant, assume the user's own tenant scope
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (string.Equals(userTenant, requestTenant, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

---

## Part 4: Authentication Registration (10 minutes)

### 4.1 Complete Auth Registration

**File:
`src/Modules/Identity/Identity.Module/Extensions/IdentityAuthExtensions.cs`**

```csharp
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

                // Map inbound claims without remapping
                JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        // Normalize Authorization header
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
```

---

## Part 5: Protecting Endpoints (5 minutes)

### 5.1 Album Endpoints with Authorization

**File: `src/Modules/Music/Music.Module/Endpoints/AlbumEndpoints.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Music.Modules.Services;

namespace Music.Modules.Endpoints;

public static class AlbumEndpoints
{
    public static void MapAlbumEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/music/albums/{id}
        group.MapGet("/albums/{id:int}", [Authorize] async (
                int id,
                IAlbumService service,
                CancellationToken ct) =>
            {
                var album = await service.GetAlbumByIdAsync(id, ct);

                return album is not null ? TypedResults.Ok(album) : Results.NotFound();
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("MusicGetAlbumById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429)
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/albums
        group.MapGet("albums/", [Authorize] async (
                IAlbumService service,
                CancellationToken ct) =>
            {
                var albums = await service.GetAllAlbumsAsync(ct);

                return Results.Json(albums);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAllAlbums")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429)
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

        // GET /api/music/albums/artist/{id}
        group.MapGet("albums/artist/{id:int}", [Authorize] async (
                int id,
                IAlbumService service,
                CancellationToken ct) =>
            {
                var albums = await service.GetAlbumsByArtistIdAsync(id, ct);

                return Results.Json(albums);
            })
            .RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")
            .WithName("GetAlbumsByArtistId")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Music")
            .Produces(429)
            .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
    }
}
```

### 5.2 Authorization Patterns

| Pattern                           | Usage                                                               |
|-----------------------------------|---------------------------------------------------------------------|
| `[Authorize]`                     | Attribute on handler - requires any authenticated user              |
| `.RequireAuthorization()`         | Fluent - requires any authenticated user                            |
| `.RequireAuthorization("policy")` | Fluent - requires specific policy                                   |
| `.AllowAnonymous()`               | Allows unauthenticated access                                       |
| Multiple policies                 | Chain with `.RequireAuthorization("p1").RequireAuthorization("p2")` |

---

## Testing Authentication

### 1. Login to Get Token

```bash
curl -X POST http://localhost:5043/api/identity/login \
  -H "Content-Type: application/json" \
  -d '{"username":"demo","password":"demo123!"}'
```

Response:

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_at_utc": "2024-01-15T11:00:00Z",
  "refresh_token": "abc123..."
}
```

### 2. Access Protected Endpoint

```bash
curl http://localhost:5043/api/music/albums/1 \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..."
```

### 3. Test Without Token (401)

```bash
curl http://localhost:5043/api/music/albums/1
# Returns: 401 Unauthorized
```

### 4. Test Wrong Permission (403)

Login as a user without `music.read` permission and try to access the albums
endpoint.

---

## Checkpoint

Before moving to Session 5, verify:

- [ ] Understand JWT token structure
- [ ] Know how tokens are generated
- [ ] Understand policy-based authorization
- [ ] Know how tenant scoping works
- [ ] Can protect endpoints with RequireAuthorization
- [ ] Can test authenticated endpoints

---

## Quick Reference

### Demo Users

| Username | Password  | Permissions                                  |
|----------|-----------|----------------------------------------------|
| demo     | demo123!  | music.read, orders.read, administration.read |
| admin    | admin123! | All permissions                              |

### Common Authorization Patterns

```csharp
// Require any authenticated user
.RequireAuthorization()

// Require specific permission
.RequireAuthorization("music.read")

// Require multiple policies (AND)
.RequireAuthorization("music.read").RequireAuthorization("tenant.scoped")

// Allow anonymous
.AllowAnonymous()
```

---

## Next Session

In **Session 5: Repository Pattern & Data Access**, you will:

- Implement the base repository with generic CRUD
- Create entity-specific repositories
- Understand EF Core patterns for SQLite
- Handle complex relationships with split queries
