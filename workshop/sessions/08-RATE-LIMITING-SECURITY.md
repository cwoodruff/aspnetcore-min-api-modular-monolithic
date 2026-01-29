# Session 8: Rate Limiting & Security

**Duration:** 45 minutes
**Session Time:** 3:45 PM - 4:30 PM

---

## Overview

This session covers API protection with rate limiting and security best
practices. You'll learn to configure rate limiting policies, apply them to
endpoints, and understand key security considerations.

---

## Learning Objectives

By the end of this session, you will:

- Configure rate limiting middleware
- Create rate limit policies with fixed windows
- Derive partition keys from requests
- Apply policies to specific endpoints
- Understand CORS configuration
- Know security best practices

---

## Part 1: Rate Limiting Configuration (15 minutes)

### 1.1 Rate Limiting in Program.cs

**File: `src/ModularMonolith.Api/Program.cs` (rate limiting section)**

```csharp
using System.Threading.RateLimiting;
using SharedKernel.TrafficControl;

// Rate limiting configuration
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicyRegistry.Names.GlobalPublicAnon, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: PartitionKeys.FromRequest(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60, // 60 requests per 60 seconds
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
```

### 1.2 Middleware Registration

```csharp
// Rate limiter should run early in the pipeline
app.UseRateLimiter();
```

Position in pipeline:

1. `UseExceptionHandler()`
2. `UseStatusCodePages()`
3. `UseCors()`
4. **`UseRateLimiter()`** - Before auth
5. `UseAuthentication()`
6. `UseAuthorization()`

### 1.3 Fixed Window Algorithm

```
Window: 60 seconds
Permit Limit: 60 requests

Timeline:
|---- Window 1 ----|---- Window 2 ----|
|  60 requests OK  |  Resets to 0     |
|  61st = 429      |  60 requests OK  |
```

---

## Part 2: Policy Registry (10 minutes)

### 2.1 Centralized Policy Names

**File: `src/Shared/SharedKernel/TrafficControl/RateLimitPolicyRegistry.cs`**

```csharp
using Microsoft.Extensions.Configuration;

namespace SharedKernel.TrafficControl;

/// <summary>
/// Central registry for rate limiting policy names and configuration binding.
/// </summary>
public sealed class RateLimitPolicyRegistry(IConfiguration configuration)
{
    // Canonical policy names (modules should reference these names only)
    public static class Names
    {
        public const string GlobalPublicAnon = "global:public-anon";
        public const string GlobalUserStandard = "global:user-standard";
        public const string GlobalTenantStandard = "global:tenant-standard";
        public const string GlobalAdminElevated = "global:admin-elevated";
        public const string ReportingHeavy = "reporting:heavy";
    }

    /// <summary>
    /// Placeholder for future binding of policies from configuration.
    /// </summary>
    public IConfiguration Section => configuration.GetSection("RateLimiting");
}
```

### 2.2 Policy Design

| Policy                   | Purpose                      | Typical Limits |
|--------------------------|------------------------------|----------------|
| `global:public-anon`     | Anonymous/unauthenticated    | 60 req/min     |
| `global:user-standard`   | Standard authenticated users | 120 req/min    |
| `global:tenant-standard` | Per-tenant limits            | 600 req/min    |
| `global:admin-elevated`  | Admin users                  | 300 req/min    |
| `reporting:heavy`        | Heavy report endpoints       | 10 req/min     |

---

## Part 3: Partition Key Resolution (10 minutes)

### 3.1 PartitionKeys Implementation

**File: `src/Shared/SharedKernel/TrafficControl/PartitionKeys.cs`**

```csharp
using Microsoft.AspNetCore.Http;

namespace SharedKernel.TrafficControl;

/// <summary>
/// Helpers to derive a stable partition key for rate limiting from the current request.
/// </summary>
public static class PartitionKeys
{
    public static string FromRequest(HttpContext httpContext)
    {
        // Priority: API key/client_id -> tenant -> sub -> IP
        var clientId = httpContext.User.FindFirst("client_id")?.Value;
        if (!string.IsNullOrWhiteSpace(clientId)) return $"client:{Normalize(clientId)}";

        var tenant = httpContext.User.FindFirst("tenant")?.Value;
        if (!string.IsNullOrWhiteSpace(tenant)) return $"tenant:{Normalize(tenant)}";

        var sub = httpContext.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(sub)) return $"sub:{Normalize(sub)}";

        var ip = GetClientIp(httpContext);
        return $"ip:{ip}";
    }

    private static string GetClientIp(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        return ip is null ? "unknown" : ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? ip.MapToIPv4().ToString()
            : ip.ToString();
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
```

### 3.2 Partition Key Priority

```
1. client_id claim (API key/machine client)
   └─> "client:abc123"

2. tenant claim (multi-tenant)
   └─> "tenant:acme-corp"

3. sub claim (authenticated user)
   └─> "sub:user-1"

4. IP address (anonymous)
   └─> "ip:192.168.1.100"
```

---

## Part 4: Applying Rate Limiting (5 minutes)

### 4.1 Endpoint-Level Application

```csharp
// Apply rate limiting to an endpoint
group.MapGet("/health", handler)
    .Produces(429) // Document 429 response
    .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
```

### 4.2 Full Endpoint Example

**File: `src/Modules/Music/Music.Module/Endpoints/AlbumEndpoints.cs` (partial)**

```csharp
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
    .Produces(429) // Rate limiting response documented
    .RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);
```

### 4.3 Rate Limited Response

When limit exceeded:

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 30
Content-Type: application/problem+json

{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429
}
```

---

## Part 5: CORS Configuration (5 minutes)

### 5.1 CORS Setup

**File: `src/ModularMonolith.Api/Program.cs` (CORS section)**

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy
            .WithOrigins(GetAllowedOrigins())
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Later in middleware pipeline
app.UseCors("Default");

static string[] GetAllowedOrigins() =>
[
    "http://localhost:3000", "http://localhost:4200", "http://localhost:5173",
    "https://localhost:3000", "https://localhost:4200", "https://localhost:5173"
];
```

### 5.2 CORS Middleware Position

```csharp
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Default");     // <-- After exception handling, before rate limiting
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
```

---

## Part 6: Security Best Practices

### 6.1 Production Checklist

| Area               | Recommendation                        |
|--------------------|---------------------------------------|
| **HTTPS**          | Always use HTTPS in production        |
| **HSTS**           | Enable HTTP Strict Transport Security |
| **CORS**           | Restrict to known origins             |
| **Rate Limiting**  | Apply to all public endpoints         |
| **Authentication** | Use strong algorithms (RS256)         |
| **Authorization**  | Principle of least privilege          |
| **Secrets**        | Never commit secrets; use Key Vault   |
| **Headers**        | Add security headers                  |

### 6.2 Security Middleware

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
```

### 6.3 Swagger Security Configuration

```csharp
builder.Services.AddSwaggerGen(c =>
{
    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste your JWT access token only",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    c.AddSecurityDefinition("Bearer", jwtSecurityScheme);
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
```

---

## Testing Rate Limiting

### 1. Trigger Rate Limit

```bash
# Send many requests quickly
for i in {1..70}; do
  curl -s http://localhost:5043/api/music/health > /dev/null
done

# The 61st request should return 429
curl -v http://localhost:5043/api/music/health
```

### 2. Expected Response

```
< HTTP/1.1 429 Too Many Requests
< Retry-After: 30
```

### 3. Wait and Retry

After the window expires (60 seconds), requests succeed again.

---

## Checkpoint

Before moving to Session 9, verify:

- [ ] Understand fixed window rate limiting
- [ ] Know how partition keys are derived
- [ ] Can apply rate limiting to endpoints
- [ ] Understand CORS configuration
- [ ] Know production security practices

---

## Quick Reference

### Rate Limiting Patterns

```csharp
// Fixed window limiter
RateLimitPartition.GetFixedWindowLimiter(
    partitionKey: "key",
    factory: _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true
    });

// Sliding window limiter
RateLimitPartition.GetSlidingWindowLimiter(
    partitionKey: "key",
    factory: _ => new SlidingWindowRateLimiterOptions
    {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1),
        SegmentsPerWindow = 4,
        QueueLimit = 0
    });

// Token bucket limiter
RateLimitPartition.GetTokenBucketLimiter(
    partitionKey: "key",
    factory: _ => new TokenBucketRateLimiterOptions
    {
        TokenLimit = 100,
        ReplenishmentPeriod = TimeSpan.FromSeconds(10),
        TokensPerPeriod = 10,
        QueueLimit = 0
    });
```

### Endpoint Rate Limiting

```csharp
// Apply policy
.RequireRateLimiting("policy-name")

// Disable rate limiting
.DisableRateLimiting()

// Document 429 response
.Produces(429)
```

---

## Next Session

In **Session 9: Testing & Wrap-Up**, you will:

- Write integration tests with WebApplicationFactory
- Test authenticated endpoints
- Review key workshop concepts
- Q&A session
