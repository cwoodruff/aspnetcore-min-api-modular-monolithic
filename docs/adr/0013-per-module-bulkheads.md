# 0013. Per-module bulkheads inside one process

- Status: Accepted
- Date: 2026-10-01
- Phase: 5 (see `docs/upgrade-plan.md`)

## Context

All five modules run in one process. Until phase 5 they also shared every
in-process resource: one rate-limit bucket per caller for every endpoint, one
`IMemoryCache` with no size limit, unbounded background work, and no way to
tell from metrics which module was busy, failing or slow. A burst against
one module could spend the others' rate limit, evict their cache and
compete for their threads, and nobody could see which module did it.

## Decision

Each module gets its own slice of the resources that can be split in-process,
and every measurement says which module it belongs to. The module's name
(`IModule.Name`) is the key throughout.

| Bulkhead | Mechanism | Configuration |
|---|---|---|
| Rate limit | One fixed-window policy per module (`catalog:api`, `orders:api`, `admin:api`, `identity:api`, `reporting:api`), partitioned by `PartitionKeys.FromRequest`, applied once on the module's route group. `global:public-anon` covers only `GET /`. | `RateLimiting:Policies:<name>:PermitLimit` / `WindowSeconds` / `QueueLimit` (default 60 per 60 s, no queue) |
| Cache | A `MemoryCache` per module with `SizeLimit` (each entry counts 1) and its own `ICacheFacade`, resolved keyed by module. The optional L2 stays shared. | `Caching:Modules:<Module>:SizeLimit` (Catalog 1000, Orders 1000, Administration 500) |
| Background work | `ModuleWorkQueue`: a bounded `Channel` (`FullMode.Wait`) with one consumer. The outbox dispatcher runs each batch through its module's queue, so a backlog makes the producer wait instead of growing memory, and a module's background work runs one item at a time. | `Concurrency:<Module>:QueueCapacity` (default 64) |
| Expensive operations | `ModuleGate`: a `SemaphoreSlim` capping concurrent expensive work for a module (Reporting uses it from phase 6). | `Concurrency:<Module>:MaxConcurrentExpensive` (default 4) |
| Visibility | `ModuleMeter`: a `Meter` named `ModularMonolith.<Module>`; every measurement is tagged `module`. Requests by status code, cache hits and misses, outbox published, dispatched, retried and dead-lettered, work-queue depth. | — |
| Health | A health check per module `DbContext`, tagged with the module name; the host reports them at `/healthz`. | — |

No new runtime packages: `System.Diagnostics.Metrics` is the metrics source.
The optional Prometheus exporter in the plan is not added; any OpenTelemetry
or Prometheus pipeline can subscribe to the `ModularMonolith.*` meters later.

## What this does not isolate

One process is still one failure domain for everything below. These need
separate processes (extraction, phase 9), not more in-process code:

- **Memory and GC.** A module that allocates heavily triggers garbage
  collections, including gen-2 pauses, that stop every module's threads. The
  cache size limits bound only the caches.
- **The thread pool.** Blocking or CPU-heavy work in one module starves the
  shared thread pool; the work queue and gate limit only the work routed
  through them.
- **Crashes.** An unhandled exception on a background thread,
  `StackOverflowException` or `OutOfMemoryException` takes down every module.
- **The database.** All modules use one PostgreSQL server and connection
  string. Pools are per DbContext type, but the server's connections, CPU and
  locks are shared.
- **Deploys.** Every release ships and restarts all modules together; a bad
  change in one is a rollback for all.
- **Requests rejected before the endpoint.** The request counter is an
  endpoint filter, so authentication, authorization and rate-limit rejections
  are not counted per module; ASP.NET Core's own meters report those.

## Consistency

Not a boundary decision.

## Consequences

- A burst against one module returns 429 from that module only.
- Filling one module's cache cannot evict another module's entries.
- Dashboards can split traffic, cache efficiency and outbox flow by module.
- Configuration grows by a few keys per module; all have defaults.
- The `outbox.published` counter counts at publish time, before the
  publishing transaction commits, so a rolled-back publish is still counted.

## How to reverse

Apply one policy on every endpoint again, register one shared cache, and
drop the queue, gate and meter registrations. Nothing outside the module
registrations depends on them.
