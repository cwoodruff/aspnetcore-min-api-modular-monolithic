# Centralized Rate Limiting & Throttling Plan (ASP.NET Core 10 Minimal API – Modular Monolith)

**Status: Partially implemented (Option A minimal wiring active)**

This document describes a centralized, policy‑driven rate limiting and
throttling strategy for the Modular Monolith. Basic rate limiting (Option A) is
currently active with the `global:public-anon` policy applied to the root
endpoint and identity endpoints. The full centralized configuration-driven
system (Options B/C) remains available for future adoption.

---

## 1) Executive Summary

- Problem: Prevent abuse and protect capacity by enforcing fair‑use limits per
  client (user/tenant/app/IP), control bursts, and cap expensive concurrent
  work (e.g., Reporting exports).
- Where it lives: A single cross‑cutting layer (Shared/TrafficControl)
  registered by the API host. Modules only opt‑in by attaching named policies
  when mapping endpoints; they contain no limiter logic.
- Opt‑in model: Modules apply `.RequireRateLimiting("{policy-name}")` or opt out
  with `.DisableRateLimiting()` for endpoints that should not be limited (e.g.,
  health).

---

## 2) Architecture Overview (described in words)

- Request flow: client → (optional Edge/APIM/Ingress) → API Host → Rate Limiter
  Middleware → Authentication → Authorization/Endpoint Handler.
- Identity integration: If authenticated, the limiter derives a partition key
  from JWT claims in priority order (client_id → tenant → sub). If anonymous, it
  falls back to IP (X‑Forwarded‑For aware).
- Two‑tier model: L1 in‑app rate limiter (ASP.NET Core middleware) for
  low‑latency control; optional L2 at the edge (NGINX/APIM/Ingress) for global
  enforcement and analytics. Policies are coordinated but layered.

---

## 3) Policy Model & Naming

Reusable named policies:

- global:public-anon — small burst, low sustained, IP‑keyed. For public
  anonymous GETs (docs, health if limited at all).
- global:user-standard — moderate burst/sustained, keyed by `sub` claim.
- global:tenant-standard — higher allowances, keyed by `tenant` claim to shield
  from noisy neighbors across users.
- global:admin-elevated — high allowances for admins, keyed by `sub`.
- reporting:heavy — concurrency limiter (max N concurrent), optional queue with
  timeout and 429 on overflow.

Naming convention:

- `{scope}:{tier}` where `scope` ∈ {global, music, orders, admin, reporting,
  identity} and `tier` describes the band (
  public-anon|user-standard|tenant-standard|admin-elevated|heavy|write-strict,
  etc.).
- Central registry (Shared/TrafficControl/RateLimitPolicyRegistry.cs) defines
  canonical names and binds them from configuration. Modules reference names
  only.

---

## 4) Identification & Partitioning Strategy

Priority order for partition keys:

1) API key or `client_id` claim (if present and trusted)
2) `tenant` claim (multi‑tenant isolation)
3) `sub` claim (user id)
4) IP address (use X‑Forwarded‑For if behind proxies; trust only configured
   proxy ranges)

Normalization & trust boundaries:

- Configure ForwardedHeadersOptions at the host to trust known proxy networks
  only; otherwise use `RemoteIpAddress`.
- Normalize keys: lower‑case, trim, restrict to ASCII; fall back safely if
  missing.

---

## 5) Algorithms & Settings

- Token bucket / sliding window for burst + sustained control on most policies.
- Fixed window can be used for simplicity where exact smoothing is not needed (
  global public‑anon).
- Concurrency limiter for long‑running endpoints (Reporting exports/queries),
  with small queue and bounded wait.
- Suggested starts (tune later):
    - public-anon: 60 req/60s with burst 20 (IP‑keyed); no queue, immediate 429
      on exceed.
    - user-standard: 600 req/60s with burst 100 (sub‑keyed).
    - tenant-standard: 3000 req/60s with burst 300 (tenant‑keyed).
    - admin-elevated: 3000 req/60s with burst 300 (sub‑keyed); apply only to
      Admin/ops endpoints.
    - reporting:heavy: concurrency 5 per tenant; queue 20; queue timeout 10s;
      429 on timeout or overflow.
- Backpressure: prefer fail‑fast 429 for public‑anon; allow bounded queue for
  heavy report endpoints. Include `Retry-After` seconds when possible.

---

## 6) Technology Options Matrix (Good/Better/Best)

- In‑app limiter only (Good):
    - Latency: excellent; Consistency: per‑node; Scale: horizontal (but counters
      not shared); Complexity: low; Cost: minimal.
- In‑app + Redis counters (Better):
    - Latency: low‑moderate; Consistency: coordinated across instances; Scale:
      high; Complexity: medium (Redis ops); Cost: Redis.
- Edge gateway/APIM + in‑app guardrails (Best):
    - Latency: low; Consistency: global at edge; Scale: very high; Complexity:
      higher (edge config + app); Cost: gateway.

Recommendation: Start with in‑app named policies (Good). For production scale,
move to Better by enabling Redis‑backed counters. For internet‑facing, consider
Best with APIM/Ingress plus light in‑app guardrails.

---

## 7) Configuration & Environments

Add `RateLimiting` configuration section:

- Provider: InMemory | Redis | EdgeOnly
- ForwardedHeaders:TrustedNetworks: ["10.0.0.0/8", "192.168.0.0/16", "fd00::/8"]
- Policies: per named policy with window, permitLimit, burst, queueLimit,
  queueProcessingOrder, concurrency, retryAfterHeader.
- Feature flags: `RateLimiting:Enabled` and per‑policy `Enabled`.
- Dev defaults: high limits or disabled; Prod: realistic values.

Example (conceptual):

```
RateLimiting:
  Enabled: true
  Provider: InMemory # or Redis
  Redis:
    ConnectionString: "<redis-conn>"
    KeyPrefix: "ratelimit:"
  Policies:
    global:public-anon:
      algorithm: SlidingWindow
      window: 60s
      permitLimit: 60
      burst: 20
      partitionKey: Ip
    global:user-standard:
      algorithm: TokenBucket
      replenishPeriod: 1s
      tokensPerPeriod: 10
      tokenLimit: 100
      partitionKey: Sub
    reporting:heavy:
      algorithm: Concurrency
      maxConcurrent: 5
      queueLimit: 20
      queueProcessingOrder: OldestFirst
      partitionKey: Tenant
```

---

## 8) Developer Experience & Module Usage

- Modules attach policies by name when mapping endpoints:
    - Music: album reads → `.RequireRateLimiting("music:user-standard")`
    - Orders: writes → `.RequireRateLimiting("orders:write-strict")`
    - Reporting: exports → `.RequireRateLimiting("reporting:heavy")`
- Exemptions: health and JWKS can call `.DisableRateLimiting()`.
- 429 response: return RFC‑9457 ProblemDetails with `type`, `title`, `status`,
  `detail`, and include headers: `RateLimit-Limit`, `RateLimit-Remaining`,
  `RateLimit-Reset`, optional `Retry-After`.

---

## 9) Observability & Ops

- Metrics: per‑policy request count, permitted vs. limited, queue wait time,
  active concurrency, top partitions.
- Logs: minimal PII; include correlation/trace IDs; sample 429s with
  policy/partition.
- Dashboards: 429 rate by policy, hot keys, latency impact from queuing.
- Alerts: >1% 429 on any policy for 5 min; queue timeout spikes; Redis errors.
- Health checks: Redis reachable (if used); drift detector for multi‑instance
  counters.

---

## 10) Testing & Validation

- Contract tests per policy: window refill behavior, burst handling, queue
  timeouts, concurrency caps.
- Integration tests: multi‑instance simulation; forwarded headers correctness;
  JWT partition key extraction.
- Chaos: Redis outage (graceful degrade to per‑node), clock skew, misconfigured
  proxies.
- Load: hot partition keys and burst surges.

---

## 11) Rollout Plan (no breaking refactors)

1) Phase 1: Implement central registry and global `public-anon` in log‑only;
   wire middleware off by default.
2) Phase 2: Enable enforcement for one module (Reporting heavy endpoints) with
   concurrency limits; monitor.
3) Phase 3: Introduce tenant/user policies; broaden to other modules; tune from
   telemetry.
4) Phase 4: Optional Redis coordination; then add edge limits in APIM/Ingress
   for internet‑facing routes.

---

## 12) Risk Register & Mitigations

- False positives throttling critical flows → allow‑lists, emergency kill switch
  per policy, and staged rollout.
- Partition key spoofing via headers → trust only known proxies; derive keys
  from JWT or RemoteIpAddress.
- Instance drift without distributed counters → prefer Redis in multi‑instance;
  or sticky sessions if unavoidable.
- Latency spikes from queues → cap queue, set timeouts, prefer fail‑fast for
  public policies.
- Misconfiguration → feature flags and safe defaults; CI validation of config
  schema.

---

## Scaffolding (names only; centralized code)

- /src/Shared/SharedKernel/TrafficControl/RateLimitingExtensions.cs
    - AddRateLimiting(this IServiceCollection, IConfiguration)
    - UseRateLimiting(this WebApplication)
    - Responsibility: register middleware/policies and standard 429 headers
      writer.
- /src/Shared/SharedKernel/TrafficControl/RateLimitPolicyRegistry.cs
    - Central registry to build named policies from configuration; exposes
      constants for common policy names.
- /src/Shared/SharedKernel/TrafficControl/PartitionKeys.cs
    - Helpers to derive partition keys from HttpContext (API key/client_id,
      tenant, sub, IP) with Forwarded‑For awareness.

## Configuration keys (catalog)

- RateLimiting:Enabled (bool)
- RateLimiting:Provider (InMemory|Redis|EdgeOnly)
- RateLimiting:Policies:* (algorithm, window, permitLimit, burst, queueLimit,
  queueProcessingOrder, concurrency, retryAfterHeader, partitionKey)
- RateLimiting:ForwardedHeaders:TrustedNetworks:*
- RateLimiting:Redis:ConnectionString, RateLimiting:Redis:KeyPrefix

## OpenAPI notes

- Document 429 responses and headers in OpenAPI. For Swagger UI, add a reusable
  response for 429 with rate limit headers; annotate representative endpoints.

## Non‑Goals

- Embedding limiter logic in business modules.
- Hard‑coding per‑customer limits in code (use configuration and, eventually,
  data‑driven policies).
- Rate limiting sensitive auth endpoints without careful exceptions.

---

## Option A: Minimal Wiring (Implemented)

This repository now includes a minimal, working rate limiter configuration wired
directly in `Program.cs` without the full centralized binding layer.

- Policy added: `global:public-anon` using a Fixed Window limiter of 60 requests
  per 60 seconds, keyed by `PartitionKeys.FromRequest(context)`.
- Middleware: `app.UseRateLimiter()` is active early in the pipeline.
- Endpoint application: the root endpoint (`GET /`) uses
  `.RequireRateLimiting("global:public-anon")`.

Code excerpts

Service registration (before `builder.Build()`):

```csharp
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using SharedKernel.TrafficControl;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicyRegistry.Names.GlobalPublicAnon, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: PartitionKeys.FromRequest(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
```

Middleware (after `builder.Build()` and before mapping endpoints/auth):

```csharp
app.UseRateLimiter();
```

Endpoint usage:

```csharp
app.MapGet("/", handler)
   .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);
```

How to extend

- Apply the same policy to more endpoints via
  `.RequireRateLimiting("global:public-anon")`.
- Add additional named policies inside the same `AddRateLimiter(...)` block (
  e.g., `global:user-standard`, `reporting:heavy`).
- Use `.DisableRateLimiting()` on endpoints that must be exempt (e.g.,
  health/JWKS) if you start applying policies broadly.

Next step toward centralization

- Migrate policy construction into
  `SharedKernel.TrafficControl.RateLimitingExtensions.AddRateLimiting(...)` to
  bind from configuration and standardize 429 responses and headers. Then switch
  `Program.cs` to call your centralized extension.
