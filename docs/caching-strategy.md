# Central Caching Strategy for ASP.NET Core 10 Modular Monolith

**Status: Implemented (L1 cache with L2 optional)**

This document describes the central caching layer that all modules (Music, Orders, Administration, Reporting, Identity) can use without cross-module coupling. The core facade and registration are implemented in `SharedKernel.Caching`.

## Current Implementation (2026-01)

The following components are implemented in `src/Shared/SharedKernel/Caching/`:
- `ICacheFacade` - Main abstraction for cache operations (`GetOrAddAsync`, etc.)
- `CompositeCacheFacade` - Two-tier implementation (L1 + optional L2)
- `IL1Cache`, `L1MemoryCacheAdapter` - In-memory cache adapter
- `IL2Cache`, `L2DistributedCacheAdapter` - Distributed cache adapter (e.g., Redis)
- `ICacheKeyComposer`, `CacheKeyComposer` - Namespaced key composition
- `CacheOptions` - Configuration binding
- `CachingRegistration.AddCentralCaching()` - DI registration extension

Configuration keys (in `appsettings.json`):
- `Caching:Tier` = "L1" (default) or "L1L2"
- `Caching:Provider` = "InMemory" (default) or configure Redis separately

Example usage in Music module (AlbumEndpoints.cs):
```csharp
var cacheKey = keyComposer.Compose("music", "album", "v1", discriminator: $"by-id:{id}");
var album = await cache.GetOrAddAsync(cacheKey, async ct => await repo.GetByIdAsync(id, ct), ct);
```

Non-goals
- Caching mutable security artifacts (access tokens, refresh tokens)

---

## Executive Summary
We will implement a central cache façade that each module can call for data and policy caching while preserving strict module boundaries. The façade will support a two-tier strategy by default: in-process L1 (IMemoryCache) for ultra-low latency and a shared distributed L2 (e.g., Redis) for consistency across instances. Namespaced keys and policy-driven TTLs ensure each module manages its own cache entries without coupling to others.

The design starts simple for local development (L1 only) and can be incrementally enabled in higher environments (L1 + L2). It supports cache-aside now, with options to extend to read-through/write-through, stale-while-revalidate, stampede protection, and event-driven invalidation later. Security guidance prohibits caching PII/secrets and sets strict TTLs for sensitive-but-cacheable data (e.g., permissions materialized from roles).

---

## 1) Decision Record (ADR-style)
- Context: Multi-instance modular monolith, minimal APIs, cloud hosting, modules with independent concerns. Need shared caching to improve latency, reduce DB load, and enable cross-instance coherence.
- Decision: Introduce a central caching abstraction with a two-tier (L1 + L2) design, namespaced per module. Default policy: cache-aside with policy-driven TTLs, optional SWR (stale-while-revalidate) and stampede controls for hot keys. L2 provider default: Redis (managed offering in cloud). Keep provider pluggable via configuration.
- Alternatives considered:
  1. Pure in-memory per module: simplest but no cross-instance consistency; not viable for scale-out.
  2. Distributed-only (no L1): consistent across instances but higher latency and cost; L1 improves P99.
  3. Two-level hybrid (chosen): balances latency and coherence; widely adopted pattern.
  4. HTTP output caching only: helpful for idempotent GETs but doesn’t address data-layer reuse or cross-endpoint caching; also limited by per-route policy.
- Rationale: Hybrid provides best latency/scale profile, works in multi-instance, and can be adopted incrementally without forcing refactors. Namespaced keys maintain module independence.

---

## 2) Architecture Overview (described in words)
- Request flow: Client → API Host → (optional) Output Caching Middleware → Module endpoint → Central Cache Facade (L1 lookup → L2 lookup → data source) → Response.
- Cache lookup path:
  - Check L1 (IMemoryCache). On hit, return value.
  - On miss, check L2 (IDistributedCache/Redis). On hit, hydrate L1, return value.
  - On miss, call data source (DB/service). Optionally apply single-flight to prevent stampede. Store into L2 then L1 with policy TTL.
- Invalidation paths:
  - Synchronous invalidation: On writes in a module, evict/purge affected keys (tagged or listed).
  - Versioned key prefixes: Rolling version increments to invalidate entire datasets with zero downtime.
  - Future: Event-driven invalidation via bus topics; consumers purge matching tags.
- Module-level policies: Each module provides caching policies (default TTLs, size quotas, tags). The central facade enforces these policies using namespacing and default conventions; modules can override via options.

---

## 3) Central Cache Abstraction (façade responsibilities)
Responsibilities
- Provide a single service API for modules to:
  - Get/Set/Remove values (cache-aside helpers).
  - Configure key namespaces, default TTLs, and max sizes per module.
  - Opt-in features: stale-while-revalidate (SWR), background refresh, stampede protection, and tagging for bulk invalidation.
- Boundary:
  - Modules do not know the underlying cache provider; they interact only with the facade using their module namespace and policies.

Namespacing strategy (convention)
- Key prefix: {env}:{app}:{module}:{entity}:{version}:{tenant?}:{locale?}:{feature?}:{id or hash}
- Example keys:
  - prod:mmapi:music:album:v3:tenantA:en-US::by-id:42
  - prod:mmapi:orders:order:v1:tenantA:::by-id:ORD-2025-000123
  - prod:mmapi:reporting:top-sellers:v2:tenantA:en-US::period:2025-W40
  - prod:mmapi:administration:config:v5::::active-flags
  - prod:mmapi:identity:permissions:v3:tenantA:::user:U-556677

Key schema guidance
- module: one of music|orders|administration|reporting|identity
- entity: logical data shape (album, order, permissions, config, report)
- version: monotonic integer to bust incompatible cached shapes without purging all keys
- tenant/locale: included for multi-tenant or localized data
- feature flag: optional bucket to isolate experiments
- id/hash: the unique discriminator; use a short deterministic hash for composite keys

Supported patterns
- Now: cache-aside (preferred), output caching at HTTP layer when appropriate.
- Later (optional): read-through adapters, write-through/write-behind for very hot paths or aggregates with complex writes.

TTL/SLAs and size quotas (defaults; override per module)
- Music: list pages 2–5 minutes; album by id 10–30 minutes; reference catalogs 1–6 hours.
- Orders: read models 15–60 seconds; order by id 30–120 seconds; avoid caching post/put results.
- Administration: feature flags/config 1–10 minutes (or event-driven); secrets never cached.
- Reporting: aggregated reports 5–30 minutes; dashboards 30–120 seconds if near-real-time.
- Identity: permission materialization 1–5 minutes; user profile fragments max 5 minutes; tokens never cached.
- Quotas: set a per-module memory budget fraction (e.g., Music 25%, Orders 25%, Reporting 25%, Admin+Identity 25% combined) when using L1; L2 sizing per capacity planning.

SWR and background refresh
- Serve cached data past TTL for a brief window (stale-if-error up to N seconds), while triggering a background refresh. Use conservative SWR in Orders (e.g., 5–10s) and looser in Reporting (e.g., 60–120s).

Stampede protection
- Single-flight per key: only one in-flight refresh computes the value; others await result or receive stale copy.
- Jitter: apply random TTL jitter (±10%) to avoid thundering herds on synchronized expiry.

Tagging and bulk invalidation
- Assign tags (sets) to keys (e.g., orders:by-customer:tenantA:42). Maintain reverse index in L2 to support bulk evictions by tag.
- Bulk purge affordances: purge by module, by entity, by tenant, by tag, or by version bump.

Warm-up hooks
- Provide a warm-start routine to pre-populate hot keys (e.g., admin config, permissions for admin user, top dashboards) at app start or via a scheduled job.

---

## 4) Technology Options Matrix
Comparison dimensions: latency, consistency, horizontal scale, multi-instance suitability, ops complexity, cost.

- IMemoryCache (L1)
  - Latency: excellent (nanoseconds–microseconds)
  - Consistency: per-instance only
  - Scale: N/A across instances
  - Multi-instance: no consistency
  - Ops: minimal
  - Cost: negligible

- IDistributedCache + Redis (L2)
  - Latency: good (sub-ms to few ms)
  - Consistency: eventual; single writer model with TTL
  - Scale: excellent (clustered Redis)
  - Multi-instance: yes
  - Ops: moderate (managed Redis reduces burden)
  - Cost: moderate

- IDistributedCache + SQL Server
  - Latency: moderate (ms–tens of ms)
  - Consistency: strong per write
  - Scale: limited vs Redis
  - Multi-instance: yes
  - Ops: moderate
  - Cost: moderate

- NCache (commercial)
  - Latency: good
  - Consistency: configurable
  - Scale: good
  - Multi-instance: yes
  - Ops: moderate–high
  - Cost: high (licenses)

Default recommendation & tiers
- Good (local/dev): L1 only (IMemoryCache). Simple and fast.
- Better (most prod): Two-tier L1 + L2 (Redis). Adds coherence and scale.
- Best (high scale): Two-tier + partitioned keys (by tenant/region), tagging, event-driven invalidation (e.g., via Service Bus/Event Grid/Kafka).

---

## 5) HTTP Caching Guidance
- When to use Output Caching: for idempotent GET endpoints with predictable variation keys (route, query, headers). Great for public data and high QPS read endpoints.
- Data caching vs output caching: use data caching for reusable data across endpoints or complex compositions; avoid double caching the same layer unless carefully tuned.
- Validators: Prefer ETag/If-None-Match and Last-Modified/If-Modified-Since for client-side caching.
- Cache-Control headers: set explicit max-age, s-maxage for CDNs, and no-store for sensitive responses.
- Avoid pitfalls: if Output Cache is enabled for an endpoint that already uses data cache, ensure distinct TTLs and cache keys to prevent stale amplification.

---

## 6) Invalidation Strategy
Triggers per module (examples)
- Music: album update, price change, artist rename → evict album/by-id and related lists (by tags).
- Orders: order status change, shipment update → evict order/by-id and customer order lists; keep TTL short.
- Administration: feature flag flip, config update → bump version or evict by tag.
- Reporting: new data ingestion, ETL run → evict report aggregates by time bucket tags.
- Identity: role/permission update, user disabled → evict permission materialization for affected users and tenant.

Mechanisms
- Event-driven invalidation (future): publish domain events; a background subscriber purges by tag/prefix.
- Versioned keys: bump {version} component to invalidate whole groups without scanning keys (zero-downtime busting).
- Tenant/region-aware invalidation: include {tenant} and {region} in keys; purge specific segments when needed.

---

## 7) Security & Compliance
- Never cache: secrets, passwords, access tokens, refresh tokens, OTPs, PII-heavy payloads unless explicitly allowed with strict TTL.
- Sensitive-but-cacheable: permission sets, feature flags, anonymized analytics aggregates; use short TTLs and encryption at rest in L2.
- Distributed cache transport: require TLS for Redis; enable AUTH and network ACLs/VNet integration.
- Data classification: define cacheable classes per module; default to non-cacheable unless explicitly allowed.
- Identity specifics:
  - Do not cache JWTs, refresh tokens.
  - Cache permission materialization per user/tenant with TTL ≤ 5 minutes.
  - On role/permission change, evict affected users immediately.

---

## 8) Observability & Ops
- Metrics: hit rate, miss rate, fill rate, latency (p50/p90/p99), eviction count, memory pressure, keyspace cardinality, lock wait (single-flight), SWR served count.
- Logs: structured logs for cache errors, stampede events, large payload warnings; never log values, only keys/hashes.
- Traces: OpenTelemetry spans around cache operations and data source fetches.
- Alerts: hit rate < 60% on hot paths; Redis error rate spike; p99 latency > target; high lock contention.
- Health checks: Redis connectivity, key age threshold for hot keys, warm-start completeness.
- Warm-start/restore: optional preloading of hot keys; snapshotting not necessary for ephemeral caches.

---

## 9) Configuration & Environments
- Dev/CI: L1 only, or ephemeral single-node Redis optional.
- Staging/Prod: L1 + managed Redis (e.g., Azure Cache for Redis Standard/Premium). Enable TLS, AUTH, and private networking.
- Config toggles (no code changes required to switch providers later):
  - Caching:Enabled = true|false
  - Caching:Tier = L1|L1L2
  - Caching:Provider = InMemory|Redis|SqlServer|NCache
  - Caching:DefaultTTLSeconds = 300
  - Caching:PerModule:Music:DefaultTTLSeconds, etc.
  - Caching:Redis:ConnectionString, Caching:Redis:InstanceName
  - Caching:Partitioning:TenantAware = true|false, RegionAware = true|false
  - Caching:SWR:Enabled, Caching:SWR:MaxStaleSeconds
  - Caching:Stampede:SingleFlight = true|false
- Capacity planning:
  - Start with working set estimates for top N keys per module; allocate Redis memory with 30–50% headroom.
  - Choose an eviction policy (allkeys-lru or volatile-lru) consistent with TTL usage.

---

## 10) Testing & Validation Plan
- Unit/contract tests against the facade: define expected behaviors for GetOrAddAsync, RemoveByTag, SWR, and single-flight semantics using a fake L2.
- Integration tests: run API with ephemeral Redis (test container); measure hit/miss flows and ensure cross-instance coherence.
- Chaos testing: inject Redis outages/timeouts; ensure graceful degradation to L1, proper fallbacks, and error budgets.
- Load tests: simulate hot keys and thundering herds; verify stampede protections and P99 latencies.

---

## 11) Adoption Plan
- Phase 1: Introduce central facade library and register it. ✓ Complete (L1 only by default)
- Phase 2: Opt-in Music module with read-only scenarios using cache-aside. ✓ Complete (Album endpoint cached)
- Phase 3: Enable L2 Redis in staging/prod; monitor hit rates and latencies; tune TTLs. (Pending - set `Caching:Tier=L1L2` and configure Redis)
- Phase 4: Add SWR and single-flight on hot paths (e.g., top dashboards, catalog lists). (Pending)
- Phase 5: Add event-driven invalidation; implement versioned prefixes and tag-based bulk purge tooling. (Pending)

---

## 12) Risk Register & Mitigations
- Consistency gaps: use short TTLs for volatile data (Orders), favor read-through for critical reads if needed.
- Sensitive data leakage: default-deny caching; security reviews for new cacheable shapes; automated scans for key patterns.
- Key collisions: strict key schema with namespacing; central helper to compose keys.
- Runaway memory use: per-module quotas; Redis maxmemory with eviction; monitor memory pressure.
- Redis outages: degrade to L1-only; circuit breakers; fail open for non-critical reads or fail closed for critical security data.
- Split-brain across instances: rely on L2 as source of truth for cache coherence; keep L1 TTLs shorter than L2.
- Stampede: single-flight locks and TTL jitter; SWR to serve stale during recomputation.

---

## 13) Technology Options Matrix (Condensed Table-in-Words)
- Good: IMemoryCache only; zero ops; for dev/local and low-risk demos.
- Better: IMemoryCache + Redis; managed service; standard choice for production.
- Best: IMemoryCache + Redis with partitioning, tagging, and event-driven invalidation for scale.

---

## 14) Module Naming/TTL Conventions (Examples)
- Music
  - Key: prod:mmapi:music:album:v3::en-US::by-id:{albumId}
  - TTL: 20 minutes for album; 3 minutes for list pages
- Orders
  - Key: prod:mmapi:orders:order:v1:{tenant}:::by-id:{orderNo}
  - TTL: 60 seconds; SWR 10 seconds
- Administration
  - Key: prod:mmapi:administration:config:v5::::active-flags
  - TTL: 5 minutes; event-driven bust where possible
- Reporting
  - Key: prod:mmapi:reporting:top-sellers:v2:{tenant}:{locale}::period:{yyyy-MM}
  - TTL: 15 minutes; background refresh every 10 minutes
- Identity
  - Key: prod:mmapi:identity:permissions:v3:{tenant}:::user:{userId}
  - TTL: 3 minutes; purge on role change

---

## 15) Discovery Questions (answered with assumptions; please validate)
A) Data & Workload
- Hot vs cold: Reporting aggregates and Music catalog lists are hot; Orders by-id reads are moderate; Admin configs moderate; Identity permissions occasionally hot. QPS: 50–300 QPS per hot endpoint in prod (assumption).
- Payload sizes: 0.5–50 KB typical; JSON serialization preferred; use gzip/brotli over the wire.
- Acceptable staleness: Orders 0–60s; Music lists 2–5m; Reporting 5–30m; Admin config 1–10m; Identity permissions ≤5m.
- Read/write ratio: Reporting read-mostly (100:1); Music lists read-heavy (50:1); Orders mixed (5:1); Admin/Identity low volume.
- Multi-tenant/region: Yes (assumption); include tenant and region in keys; enforce per-tenant quotas if needed.

B) Consistency & Invalidation
- Orders write reflection: user-facing reads tolerate up to 60s staleness (assumption); critical flows can bypass cache.
- Invalidation hooks: domain events published on changes (assumption); fallback to explicit evictions on write paths.
- Cross-module invalidation: Admin feature flags can affect other modules; use tags and/or version bump.

C) Deployment & Ops
- Environments: local dev, CI, preview, prod; containerized; deploy to K8s or Azure App Service.
- Managed cache: Azure Cache for Redis (Standard/Premium) preferred in cloud.
- RTO/RPO: cache is ephemeral; RPO=0; RTO minimal; warm-start optional.

D) Security & Compliance
- Sensitive fields: PII (email, addresses), payment data; do not cache or use strict TTL and redaction if absolutely needed.
- Identity caching: only permission materialization and small profile fragments; TTL ≤ 5 minutes; purge on role/profile change.

E) Tooling & Observability
- Platform: OpenTelemetry for traces; metrics to App Insights or Prometheus; alerts via Azure Monitor/Alertmanager.
- Distributed tracing across cache and DB calls: yes.

F) Product & UX
- SWR: acceptable brief staleness for lists and dashboards; not for order status during checkout.
- SLAs: public GETs may use output caching for 99th percentile latency targets.

---

## 16) Configuration Keys (proposed; portable)
- Caching:Enabled
- Caching:Tier (L1|L1L2)
- Caching:Provider (InMemory|Redis|SqlServer|NCache)
- Caching:DefaultTTLSeconds
- Caching:PerModule:{Module}:DefaultTTLSeconds
- Caching:SWR:Enabled, Caching:SWR:MaxStaleSeconds
- Caching:Stampede:SingleFlight
- Caching:Redis:ConnectionString, InstanceName, PoolSize, SSL=true
- Caching:Partitioning:TenantAware, RegionAware

---

## 17) Operational Playbooks
- Redis outage: switch Tier to L1, Provider to InMemory; degrade gracefully.
- Hot key incident: add jitter, enable SWR, split keys by partition (tenant/region) temporarily.
- Stale data complaint: bump version prefix for affected entity; schedule event-driven invalidation rollout.

---

## 18) Short ADR (Appendix)
- Title: Adopt Two-Tier Central Cache with Namespaced Keys
- Status: Proposed
- Context: Multi-instance modular monolith requiring low latency and coherence.
- Decision: Use a central cache facade with IMemoryCache (L1) + Redis (L2), cache-aside pattern, namespaced keys, policy-driven TTLs, optional SWR and stampede protection.
- Consequences: Improved latency and scale; added operational dependency on Redis in prod; requires discipline for key naming and invalidation policies.
