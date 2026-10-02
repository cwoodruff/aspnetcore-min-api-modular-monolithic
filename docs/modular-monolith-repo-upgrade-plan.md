# Upgrade plan: aspnetcore-min-api-modular-monolithic

Repo: github.com/cwoodruff/aspnetcore-min-api-modular-monolithic
Baseline: main at commit 42199a3 (ASP.NET Core 10, net10.0, 271 tests green)
Goal: implement every fix the second video promised, in the order that keeps the build green and each step reviewable.

This document is written for Claude Code. Read all of it before starting phase 0. Each phase is one branch and one PR. Do not start a phase until the previous phase's acceptance criteria pass on main.

---

## 1. Why these changes

The second video ("The Modular Monolith, Honestly") walked through this repo and named the places where it breaks its own rules. This plan fixes each one.

| # | Problem in the repo today | Where the video calls it out | Fixed in phase |
|---|---|---|---|
| 1 | One `AppDbContext` in `SharedKernel.Persistence` holds all eleven entities, with navigation properties that cross module lines (`Invoice.Customer`, `InvoiceLine.Track`, `Track.Genre`, `Track.MediaType`) | Ch. 2 and 4 | 2 |
| 2 | The shared kernel owns the domain: every entity, API model, validator, repository interface and implementation lives in `SharedKernel.Persistence` and `SharedKernel.DataSQLite`. Modules own only endpoints and thin services | Ch. 4 | 2 |
| 3 | `Program.cs` registers all ten repositories against interfaces in the shared kernel, so any module can inject any other module's repository. Nothing but code review stops it | Ch. 4 | 2, 4 |
| 4 | Authorization policy names are strings owned by Identity and referenced by every other module. A rename drifted for one commit and only integration tests caught it | Ch. 4 | 0, 4 |
| 5 | No architecture test reads `InternalsVisibleTo` and fails on a second entry | Ch. 4 | 0 |
| 6 | No events, no dispatcher, no outbox. Every cross-module effect would be a shared transaction by default, and the consistency decision on each boundary is unwritten | Ch. 2, 3, 7 | 3 |
| 7 | One rate-limit policy shared by every endpoint, one `IMemoryCache` with no per-module size limit, no bounded channels, no per-module metrics | Ch. 5 | 5 |
| 8 | SQLite: no schemas, so schema-per-module, per-context migration history, cross-schema FK removal and the orphan check cannot even be attempted | Ch. 6 | 1, 2, 6 |
| 9 | Reporting module is two health endpoints. The cross-module read model it exists for does not exist | Ch. 6 | 6 |
| 10 | No startup check for duplicate routes; no OpenAPI document per module; endpoint handlers are inline lambdas that cannot be unit tested | Ch. 7 | 0, 7 |
| 11 | No per-module test host. Repository tests run on the EF in-memory provider, not the real engine | Ch. 8 | 1, 8 |
| 12 | No decision records anywhere | Ch. 2 | 0, then every phase |
| 13 | Drift: `Directory.Build.props` says net9.0 while projects say net10.0; `Dockerfile` uses .NET 9 images | README | 0 |

---

## 2. Working agreement for Claude Code

These apply to every phase.

1. Simplicity-First. Prefer the smallest change that fixes the named problem. Do not add a layer, a package or an abstraction unless a phase asks for it. If you think one is needed anyway, write a one-paragraph ADR and stop for review.
2. Green at every commit. `dotnet build ModularMonolith.Api.sln` and `dotnet test ModularMonolith.Api.sln` must pass before each commit. Architecture tests are the fence; never weaken one to make a phase pass. If a rule is wrong, say so in the PR description.
3. No MediatR, no MassTransit, no AutoMapper. The event dispatcher and outbox are written by hand in this repo (phase 3 explains why and how). Allowed new packages are listed per phase; anything else needs an ADR.
4. One branch per phase, named `refactor/NN-slug` as given below. One PR per phase. PR description lists the ADRs added and the tests added.
5. Keep the module contract unchanged: `IModule { Name; RegisterServices(services, config); MapEndpoints(endpoints); }` in `SharedKernel`. Keep the explicit `GetModules()` list in `Program.cs`. No assembly scanning for modules.
6. Everything inside a module stays `internal` except its composition class and its Contracts project. `PublicSurfaceTests` is updated, never deleted.
7. Every cross-module boundary gets a decision record in `docs/adr/` before the code that implements it merges. Template in phase 0.
8. Update `README.md` and `docs/` in the same PR as the code they describe. Delete docs that describe the old shape (`docs/EFCore-Plan.md` is SQLite and single-context; rewrite it in phase 2).
9. Conventional commit messages: `feat(orders): ...`, `refactor(catalog): ...`, `test(arch): ...`, `docs(adr): ...`.
10. Measure before claiming. Where this plan says "record the numbers", run the command three times and put the median in the README.

Start each phase by reading `docs/adr/` and this file's section for the phase, then post a short plan of the files you will touch before editing.

---

## 3. Target shape

```
/src
  /ModularMonolith.Api                 host: Program.cs, explicit module list, host-only wiring
  /Modules
    /Catalog
      /Catalog.Contracts               public: integration events, DTOs other modules may read, permission names
      /Catalog.Module                  internal: CatalogDbContext (schema catalog), entities, validators, services, endpoints, migrations
    /Orders
      /Orders.Contracts
      /Orders.Module                   OrdersDbContext (schema orders), outbox table lives here
    /Administration
      /Administration.Contracts
      /Administration.Module           AdministrationDbContext (schema administration)
    /Identity
      /Identity.Contracts              permission and policy name constants
      /Identity.Module
    /Reporting
      /Reporting.Module                ReportingDbContext (schema reporting): views + integrity findings, read-only
  /Shared
    /SharedKernel                      IModule, caching facade, rate-limit registry, event dispatcher abstractions,
                                       outbox/inbox primitives, module metrics helper. Nothing domain-shaped.
/tests
  /ModularMonolith.Api.Tests           full-host integration tests, Postgres via Testcontainers, Respawn per schema
  /ModularMonolith.Module.Tests        per-module test host (one module + fakes for the Contracts it consumes)
  /ModularMonolith.Architecture.Tests  ArchUnitNET rules + reflection rules (IVT, DI, policies, routes, contracts shape)
/docs/adr                              ADR-0001 onward
/docker-compose.yml                    Postgres 17 for local dev, seeded with Chinook
```

`SharedKernel.Persistence` and `SharedKernel.DataSQLite` are deleted by the end of phase 2. The repository layer is dissolved: services use their module's `DbContext` directly. EF Core is the repository. (Rationale: `.simplicity-baseline.json` already counts 28 interfaces with a single implementation; ten of them are repositories that wrap one `DbSet` each.)

---

## Phase 0: guardrails and hygiene

Branch: `refactor/00-guardrails`

Goal: fix drift, add the decision-record scaffold, and add the architecture tests that will protect every later phase. No behaviour changes.

Changes

1. `Directory.Build.props`: `TargetFramework` to `net10.0`, and remove the per-project `<TargetFramework>` overrides so there is one source of truth. Keep `TreatWarningsAsErrors`.
2. `Dockerfile`: `mcr.microsoft.com/dotnet/sdk:10.0` and `aspnet:10.0`. Remove the drift note from README.
3. Create `docs/adr/README.md` and `docs/adr/0000-template.md` with these sections: Context, Decision, Consistency (for boundaries: "shared transaction", "outbox, eventual", or "read only"), Consequences, How to reverse. Add `docs/adr/0001-module-map-and-ownership.md` recording today's five modules and which entities each owns (Catalog: Artist, Album, Track, Playlist, PlaylistTrack. Orders: Invoice, InvoiceLine. Administration: Customer, Employee, Genre, MediaType. Identity: no persisted entities today. Reporting: none).
4. Architecture tests, new file `tests/ModularMonolith.Architecture.Tests/GuardrailTests.cs`:
   - `Module_Has_Exactly_One_InternalsVisibleTo_And_It_Is_Its_Own_Test_Project`: reflect over each module assembly's `InternalsVisibleToAttribute` instances; assert count is 1 and the name is the allowed test assembly for that module (a static map in `ArchitectureConstants`).
   - `No_Module_Service_Resolves_To_Another_Modules_Type`: build the real `IServiceCollection` the way `Program.cs` does (extract the registration code into a `HostComposition.ConfigureServices(builder)` static method in the host so the test can call it), then for every `ServiceDescriptor` whose `ImplementationType` lives in module assembly A, assert no descriptor consumed by module B's assembly resolves to it. Concretely: for each module assembly, collect every constructor parameter type of its `internal` service classes; resolve each from a built provider; assert the implementation's assembly is that module, `SharedKernel`, a `*.Contracts` assembly, or a framework assembly. This test passes today because no module injects another's repository. It exists to keep it that way.
   - `Every_Authorization_Policy_Referenced_By_An_Endpoint_Is_Registered`: build the host with `WebApplicationFactory<Program>`, walk `EndpointDataSource.Endpoints`, collect every `IAuthorizeData.Policy`, and assert `IAuthorizationPolicyProvider.GetPolicyAsync(name)` is non-null for each. This is the test that would have caught the Music to Catalog rename.
   - `No_Two_Endpoints_Share_Route_And_Method`: from the same endpoint list, assert (`RoutePattern.RawText`, HTTP methods) pairs are unique.
5. Startup check in the host: `ModuleComposition.ValidateEndpoints(app)` runs after `MapEndpoints` in Development and Test environments and throws with a readable list on duplicate routes or unknown policies. Same logic as the two tests above, so the tests can call it.
6. GitHub Actions workflow `.github/workflows/dotnet.yml`: build and test on push and PR. (The existing squad workflows stay.)

Acceptance

- `dotnet test` green, count goes from 271 to at least 275.
- `docs/adr/0001-module-map-and-ownership.md` exists and matches the code.
- README drift note removed; `docker build` succeeds.

---

## Phase 1: PostgreSQL, Testcontainers, real migrations

Branch: `refactor/01-postgres`

Goal: replace the SQLite file with PostgreSQL 17, EF Core migrations, and container-backed tests, while keeping the single `AppDbContext` for one more phase. Tables land in the schema of the module that will own them, so phase 2 is a code move, not a data move.

Allowed packages: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x, `Testcontainers.PostgreSql`, `Respawn`. Remove `Microsoft.EntityFrameworkCore.Sqlite`, `SQLitePCLRaw.bundle_e_sqlite3`, `Microsoft.EntityFrameworkCore.InMemory`.

Changes

1. `docker-compose.yml` at the repo root: `postgres:17-alpine`, database `chinook`, a named volume, port 5432, healthcheck. README gets a "Run it" section: `docker compose up -d`, `dotnet run --project src/ModularMonolith.Api`.
2. Connection string key stays `ConnectionStrings:AppDatabase`. Default in `appsettings.Development.json` points at the compose container. Delete the chinook.db path-finding code in `Program.cs` and `PersistenceRegistration.cs`.
3. `AppDbContext`: `UseNpgsql`. In `OnModelCreating`, put every table in its future owner's schema now: `catalog.Artist`, `catalog.Album`, `catalog.Track`, `catalog.Playlist`, `catalog.PlaylistTrack`, `orders.Invoice`, `orders.InvoiceLine`, `administration.Customer`, `administration.Employee`, `administration.Genre`, `administration.MediaType`. Column types: replace `nvarchar(n)` with `varchar(n)`, `datetime` with `timestamp with time zone`, `decimal(10,2)` with `numeric(10,2)`.
4. Migrations: `dotnet ef migrations add InitialSchema` into `SharedKernel.Persistence/Migrations` (temporary home; it moves in phase 2). Seed data: add `data/chinook-postgres-seed.sql` derived from the Chinook PostgreSQL script with table names mapped to the schemas above, and a `DbSeeder` that runs it once when `Track` is empty (Development and Test only).
5. Tests:
   - `ModularMonolith.Api.Tests`: a shared `PostgresFixture` (`ICollectionFixture`) that starts one `PostgreSqlContainer`, applies migrations, seeds, and exposes a `Respawner` configured with `SchemasToInclude = ["catalog","orders","administration"]` and `DbAdapter.Postgres`. Each test class resets between tests. `WebApplicationFactory` gets the container connection string through `ConfigureAppConfiguration`.
   - Delete `TestDbHelpers.CreateInMemoryContext`. Repository tests run against the container.
   - Record in README: first-test boot time, per-test time, full suite time on your machine, before and after.
6. Keep SQLite out. If a zero-dependency demo is wanted later, it is a separate ADR; do not carry two providers.

ADRs: `0002-database-engine.md` (PostgreSQL; why not SQLite for this repo; why schemas matter).

Acceptance

- `docker compose up -d && dotnet run` serves `/api/catalog/data-health` with `connected: true`.
- All tests pass with no SQLite or InMemory package referenced anywhere in the solution.
- `dotnet ef migrations list` shows one migration; `psql -c "\dn"` shows the three schemas.

---

## Phase 2: one DbContext per module, domain out of the shared kernel

Branch: `refactor/02-split-contexts`

Goal: each module owns its entities, its `DbContext`, its migrations and its schema. No navigation property crosses a module line. The shared kernel goes back to being a kernel.

This is the biggest phase. Do it in this order inside the branch, committing at each step with tests green.

Step A: create the Contracts projects (empty for now) and add `ProjectReference`s: each `X.Module` references `X.Contracts` and `SharedKernel` only. Update `PublicSurfaceTests` to allow the Contracts assemblies to be fully public and the Module assemblies to export only `XModule` and `XModule.Modules` (Identity also exports `IdentityAuthExtensions`).

Step B: move entities into modules as `internal` classes, one folder `Domain/` per module. Remove the `IConvertModel<T>` interface and `Convert()` methods; mapping becomes a small static mapper in the module's `Endpoints/` or `Services/` folder. Move API models (rename to `Contracts/Dto` or keep in the module as `internal` records if no other module reads them; today none do) and FluentValidation validators into the owning module.

Step C: remove the four cross-module navigations and their inverse collections:
- `Invoice.Customer` and `Customer.Invoices` (Orders to Administration)
- `InvoiceLine.Track` and `Track.InvoiceLines` (Orders to Catalog)
- `Track.Genre` and `Genre.Tracks` (Catalog to Administration)
- `Track.MediaType` and `MediaType.Tracks` (Catalog to Administration)
Keep the `int? CustomerId`, `TrackId`, `GenreId`, `MediaTypeId` columns. They are application-validated IDs now. `InvoiceRepository.GetById` currently projects customer name, support-rep name and track names in one query; replace with an Orders-only projection plus IDs. If the endpoint response needs names, phase 3 gives Orders a way to ask for them (a read contract on the other module's Contracts project), not a join.

Step D: split the context. `CatalogDbContext` (`HasDefaultSchema("catalog")`, `MigrationsHistoryTable("__EFMigrationsHistory", "catalog")`), `OrdersDbContext`, `AdministrationDbContext`. Each module registers its own context in `RegisterServices` using the shared connection string name. Write the migration for each context so that the generated SQL only drops the four cross-schema foreign keys and otherwise matches the phase 1 schema (verify with `dotnet ef migrations script`; there must be no table recreation). Migration order on startup: `Administration`, `Catalog`, `Orders`, then `Reporting` in phase 6; the host runs `Database.Migrate()` per context in that order in Development and Test.

Step E: dissolve the repository layer. Services take their module's `DbContext`. Keep `AsNoTracking` on reads, keep the cache-aside pattern and tag names. Move all service registrations into each module's `RegisterServices`; `Program.cs` registers no repositories and no contexts. `Track.GetByInvoiceId` in Catalog was a read of Orders' data through the shared context; delete it and the endpoint `GET /api/catalog/tracks/invoice/{id}` (Orders exposes invoice lines already).

Step F: delete `SharedKernel.Persistence` and `SharedKernel.DataSQLite`. `SharedKernel` keeps `IModule`, `Caching/`, `TrafficControl/`, `BuildInfoProvider`, plus one new file `Persistence/ModuleDbContextOptions.cs` (a helper that applies `UseNpgsql` with the schema and history table so the three contexts do not repeat it).

Step G: `ModuleBoundaryTests` gains two rules:
- No type in a `*.Module` assembly depends on a type in another `*.Module` assembly (exists) and no type in `SharedKernel` depends on any module or Contracts assembly (exists), plus: no type in any `*.Contracts` assembly depends on a `*.Module` assembly.
- Each module's `DbContext` model contains only entity types declared in that module's assembly (reflection over `IModel.GetEntityTypes()`).

Step H: `.simplicity-baseline.json`: re-run the tool if you have it; otherwise record project count and interface count by hand in the PR description.

ADRs: `0003-one-dbcontext-per-module.md`, and one per removed navigation: `0004-orders-to-administration-customer.md`, `0005-orders-to-catalog-track.md`, `0006-catalog-to-administration-genre.md`, `0007-catalog-to-administration-mediatype.md`. Each records: consistency choice is "application-validated ID, no database FK, eventual", what breaks if the referenced row disappears, and that phase 6 adds the orphan check. `docs/EFCore-Plan.md` rewritten for the new shape.

Acceptance

- Solution has no project named `SharedKernel.Persistence` or `SharedKernel.DataSQLite`.
- Three contexts, three history tables, `dotnet ef migrations script` for each shows FK drops only.
- Architecture tests: the two new rules pass; `Module_Should_Not_Depend_On_Other_Module` still passes for all 20 pairs.
- All existing endpoints keep their routes and status codes; the Api.Tests suite passes with only response-shape adjustments where names were joined in.

---

## Phase 3: events, outbox, and two consumers

Branch: `refactor/03-outbox`

Goal: make branch two from the video real. Orders publishes `InvoiceFinalized` through an outbox in its own schema; a worker dispatches; Catalog and Administration handle it idempotently in their own transactions. The semantics are chosen on purpose and written down.

No new packages. Everything here is small enough to own.

Changes

1. `SharedKernel/Events/`:
   - `IIntegrationEvent { Guid EventId; DateTimeOffset OccurredAt; }`
   - `IIntegrationEventHandler<TEvent>` with `Task HandleAsync(TEvent e, CancellationToken ct)`.
   - `IEventPublisher` with `Task PublishAsync<TEvent>(TEvent e, DbContext sameTransactionAs, CancellationToken ct)`: writes a row into the publishing module's outbox table using the caller's context so it commits with the business write. No in-memory publish path. That is the point.
   - `OutboxMessage` entity shape (Id, EventType, Payload jsonb, OccurredAt, ProcessedAt, Attempts, LastError, DeadLetteredAt) and `InboxMessage` shape (EventId, HandlerName, ProcessedAt) as reusable owned configurations each module applies in its own schema.
   - `OutboxDispatcher` hosted service base: polls one module's outbox (`FOR UPDATE SKIP LOCKED`, batch of 50), deserializes by `EventType`, resolves handlers from a scoped provider, runs each handler inside `InboxGuard` (insert into the handler's module inbox first; if the row exists, skip; commit handler work and inbox row together), retries with exponential backoff (1s, 5s, 30s, 2m, 10m), moves to dead letter after 5 attempts, and exposes counters through the module metrics helper from phase 5 (stub it now, wire it then).
   - Ordering: document that ordering is per outbox row insertion order within one dispatcher, not across modules or after retries. No sequence and lock; the video says why.
2. `Orders.Contracts/Events/InvoiceFinalized.cs`: record with InvoiceId, CustomerId, InvoiceDate, Total, and `IReadOnlyList<InvoiceFinalizedLine>(TrackId, Quantity, UnitPrice)`.
3. Orders: `Invoice` gets `Status` (Draft, Finalized). New endpoint `POST /api/orders/invoices/{id}/finalize` (policy `orders.write`, `tenant.scoped`) that sets the status and publishes `InvoiceFinalized` in the same `SaveChanges`. `orders.OutboxMessage` table via migration. `OrdersOutboxDispatcher : OutboxDispatcher` registered as a hosted service in `OrdersModule.RegisterServices`.
4. Catalog consumer: `TrackSales` entity (TrackId PK, TimesSold, LastSoldAt) in `catalog` schema; handler increments per line; `catalog.InboxMessage` table. Endpoint `GET /api/catalog/tracks/{id}/sales`.
5. Administration consumer: `CustomerPurchaseSummary` (CustomerId PK, TotalSpent, InvoiceCount, LastPurchaseAt) in `administration` schema; handler updates; `administration.InboxMessage`. Endpoint `GET /api/admin/customers/{id}/purchases` (policies `role.admin`, `administration.read`, `tenant.scoped`).
6. Dead letters: `GET /api/orders/outbox/dead-letters` (policy `role.admin`) lists them; `POST /api/orders/outbox/dead-letters/{id}/retry` resets attempts. Nothing more. The video's point is that someone has to look at this table; give them a way.
7. Read-your-own-writes note: the finalize endpoint returns `202 Accepted` with a `Location` of the invoice and a body field `salesCountersUpdate: "eventual"`. Document it in the endpoint's OpenAPI description.

Tests

- `Module.Tests` (phase 8 adds the shared host; for now put these in Api.Tests): finalizing an invoice writes exactly one outbox row in the same transaction (assert by forcing a failure after the invoice write and checking neither row exists).
- Dispatcher processes the row; `TrackSales` and `CustomerPurchaseSummary` reflect it within a polling deadline (use a test dispatcher trigger, not `Task.Delay` loops).
- Delivering the same event twice updates counters once (inbox idempotency), for both consumers.
- A handler that throws is retried, then dead-lettered after 5 attempts; the other consumer is unaffected.
- Architecture: handlers for `InvoiceFinalized` live in Catalog and Administration and reference only `Orders.Contracts`, never `Orders.Module`.

ADRs: `0008-integration-events-and-outbox.md` (hand-rolled, why not MediatR: licence change and owning the semantics; at-least-once; inbox per handler; retry schedule; dead-letter policy; ordering guarantee stated narrowly), `0009-orders-to-catalog-track-sales.md` and `0010-orders-to-administration-purchase-summary.md` (consistency choice: outbox, eventual; what the UI must not promise).

Acceptance

- The finalize flow works end to end against the compose database with the dispatcher running.
- All five test scenarios above pass.
- `docs/adr/0008` is linked from README under "How modules talk to each other".

---

## Phase 4: make the fences real

Branch: `refactor/04-fences`

Goal: the boundary rules the video listed become tests or startup failures, and the string-typed coupling is moved to a compiled contract.

Changes

1. `Identity.Contracts/Permissions.cs` and `Policies.cs`: public constants (`Permissions.CatalogRead = "catalog.read"`, `Policies.TenantScoped = "tenant.scoped"`, `Policies.Admin = "role.admin"`). Every `RequireAuthorization("...")` string literal in every module becomes a constant reference. Architecture test: no string literal matching `^[a-z]+\.[a-z.]+$` appears as an argument to `RequireAuthorization` (Roslyn-free version: reflect over endpoint metadata at test time and assert each policy name equals a constant's value from `Identity.Contracts`).
2. Contracts shape rule: architecture test that every type in a `*.Contracts` assembly is an interface, an enum, a `record`, or a `static class` containing only `const` fields. No methods with bodies except record members. This is the "contracts projects catch the same disease" guard.
3. Shared kernel budget: test that `SharedKernel` has at most N public types (set N to the count after phase 3, plus 2) and depends on no `Npgsql`, `FluentValidation` or module assemblies. When someone needs to raise N, they change the test and the reviewer sees it.
4. DI: module registrations use keyed services for anything a module registers against a `SharedKernel` interface that another module also implements (today: `IIntegrationEventHandler<>` implementations). Handler resolution in the dispatcher uses the module key. Extend the phase 0 DI test to assert that resolving an open-generic handler from module A's scope never yields module B's type.
5. `InternalsVisibleTo`: confirm phase 0 test still passes after the Contracts split; Contracts projects must have none.

ADRs: `0011-authorization-names-are-compiled-contracts.md`, `0012-shared-kernel-budget.md`.

Acceptance

- Grep for `RequireAuthorization("` returns zero hits in `src/`.
- New architecture tests pass; the suite has at least 8 architecture rules beyond the original 3 files.

---

## Phase 5: one process, softened

Branch: `refactor/05-bulkheads`

Goal: the in-process mitigations from chapter 5, each one attributable to a module.

Allowed packages: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.Prometheus.AspNetCore` (optional; `System.Diagnostics.Metrics` is the source either way).

Changes

1. Rate limiting: `RateLimitPolicyRegistry` grows one named policy per module (`catalog:read`, `orders:write`, `admin:read`, and so on) with limits set in configuration under `RateLimiting:Policies:<name>`. Each module applies its own policy on its `MapGroup` rather than per endpoint. Partition key stays `PartitionKeys.FromRequest`. Keep `global:public-anon` for the root endpoint only.
2. Cache: `AddCentralCaching` becomes `AddModuleCache(moduleName, sizeLimit)` called from each module's `RegisterServices`, giving each module its own `MemoryCache` instance with `SizeLimit` and entry sizes set (count-based is fine). `ICacheFacade` is resolved keyed by module name. Cache keys are already namespaced; keep them.
3. Bounded work: `SharedKernel/Concurrency/ModuleWorkQueue` wraps a `Channel<T>` with `BoundedChannelFullMode.Wait` and a configured capacity; the outbox dispatchers and the phase 6 integrity job take their work through it. `ModuleGate` wraps `SemaphoreSlim` for expensive operations; Reporting uses it in phase 6.
4. Metrics: `SharedKernel/Diagnostics/ModuleMeter` creates a `Meter` per module and tags every instrument with `module`. Instruments: requests by status (via a small endpoint filter applied on each module group), cache hits and misses, outbox published, dispatched, retried, dead-lettered, queue depth. Expose `/metrics` in Development if the Prometheus exporter is included.
5. Health: each module's `/health` and `/data-health` stay; add ASP.NET Core `IHealthCheck` per module context so `/healthz` (host) reports per-module status with tags.

Tests

- Rate limiting test per module policy: exceeding Catalog's limit returns 429 on Catalog and 200 on Orders in the same window.
- Cache size limit test: inserting past the limit evicts within the module and does not touch another module's cache.
- Metrics test: a request to a Catalog endpoint increments an instrument tagged `module=Catalog` (use `MetricCollector<T>` from `Microsoft.Extensions.Diagnostics.Testing`).

ADR: `0013-per-module-bulkheads.md` (what is mitigated, what is not: GC pauses, thread pool, deploy blast radius).

Acceptance

- `RequireRateLimiting(` appears only in module `MapEndpoints` on groups, not per endpoint.
- Three new tests pass. README gains a "Failure domain" section listing what a module can and cannot isolate in-process.

---

## Phase 6: Reporting owns the read model, and the orphan check

Branch: `refactor/06-reporting`

Goal: the Reporting module does what it exists for, in its own schema, reading everything and writing only its own findings.

Changes

1. `ReportingDbContext` with `HasDefaultSchema("reporting")` and its own history table. Its first migration creates a view `reporting.sales_by_genre` over `catalog.Track`, `catalog.TrackSales`, `administration.Genre`, plus a view `reporting.invoice_lines_with_names` over `orders.InvoiceLine`, `catalog.Track`, `administration.Customer`. Views are raw SQL in the migration; keyless entity types map them. This is the coupling the video describes; the ADR says so and says why it is acceptable here.
2. Database role: migration creates role `reporting_reader` with `SELECT` on the `catalog`, `orders` and `administration` schemas and no write grants; the Reporting connection string in configuration uses it (`ConnectionStrings:Reporting`). Compose file creates the role.
3. Endpoints: `GET /api/reporting/sales-by-genre`, `GET /api/reporting/invoices/{id}/lines` (policy `report.view`, `tenant.scoped`), both behind a `ModuleGate` with concurrency 4 and the Reporting rate-limit policy.
4. Integrity job: `IntegrityCheckJob` hosted service, runs nightly (configurable cron via `PeriodicTimer`; no Quartz) and on demand through `POST /api/reporting/integrity/run` (`role.admin`). It executes one query per removed FK from phase 2: invoice lines whose `TrackId` has no track, invoices whose `CustomerId` has no customer, tracks whose `GenreId` or `MediaTypeId` has no row. Findings go to `reporting.IntegrityFinding` (Id, CheckName, SourceSchema, SourceTable, SourceId, MissingReference, DetectedAt, ResolvedAt). `GET /api/reporting/integrity/findings` lists open ones. The job does not repair anything.
5. Rename detection: a Reporting test that builds the two views' SQL against the current model of the other three contexts (reflect table and column names from each context's `IModel`) and fails if a referenced column no longer exists. The failure lands in Reporting's tests, which is the right place.

Tests

- Views return expected rows against seeded Chinook data.
- Deleting a track via `CatalogDbContext` (test only) and running the job produces one finding; re-running produces no duplicate.
- Reporting connection cannot `INSERT` into `catalog.Track` (expect a permission error).

ADRs: `0014-reporting-read-model-as-views.md`, `0015-orphan-detection.md`.

Acceptance

- Four contexts migrate in order on startup: administration, catalog, orders, reporting.
- Reporting module exports only its composition class; it has no Contracts project.

---

## Phase 7: Minimal API edges

Branch: `refactor/07-minimal-api-edges`

Goal: the chapter 7 items that are not already covered by phase 0.

Changes

1. Handlers out of lambdas: every endpoint maps to a `static` method on an `internal static class XHandlers` in the module (`group.MapGet("/albums/{id:int}", AlbumHandlers.GetById)`). Handlers return `Task<Results<Ok<T>, NotFound>>` style typed results so OpenAPI is exact.
2. Validation: keep the current shape (module validates and throws; host maps to RFC 7807), but add a `ValidationFilter<T>` endpoint filter in `SharedKernel` for write endpoints so validation happens before the handler runs, and the exception path becomes the fallback. One error shape, tested once in the host tests.
3. Filter ordering: one test that adds a marker filter on a group and on an endpoint and asserts execution order, so the documented rule is checked, not remembered.
4. OpenAPI per module: `AddSwaggerGen` defines one document per module (`catalog`, `orders`, `admin`, `identity`, `reporting`) using `DocInclusionPredicate` on the module tag, plus the combined `v1`. Swagger UI lists all of them.
5. Native AOT readiness (no publish change): add `<IsAotCompatible>true</IsAotCompatible>` to `SharedKernel` and every `*.Contracts` project and fix the warnings. Modules and host stay off for now; note in README why (Swashbuckle, FluentValidation reflection).

Tests

- Handler unit tests for two handlers per module, no host, fake service.
- Swagger document per module contains only that module's paths.
- Filter order test.

Acceptance

- Zero inline lambda handlers in `src/Modules/**/Endpoints`.
- `dotnet build` warns on nothing in `SharedKernel` and `*.Contracts` with AOT analysis on.

---

## Phase 8: a test host per module, and the numbers

Branch: `refactor/08-module-test-host`

Goal: the test shape the video said it did not have.

Changes

1. New project `tests/ModularMonolith.Module.Tests`. `ModuleTestHost<TModule>` builds a `WebApplication` that registers `SharedKernel` services, the one module, the phase 1 Postgres fixture (schema reset limited to that module's schema), and fakes for every `*.Contracts` interface the module consumes (none today beyond events; register a recording `IEventPublisher` and an in-memory dispatcher trigger).
2. Move service-level and handler-level tests from `Services.Tests` into `Module.Tests`, organized by module. Delete `Services.Tests` when empty.
3. Api.Tests keeps only cross-module flows: finalize an invoice and see both consumers update; authorization pipeline; rate limiting across modules; health.
4. Timing: a small `tests/timing.ps1` and `timing.sh` that run each test project three times and print median wall-clock. Put the medians in README under "Test cost", with the machine described.

Acceptance

- `Module.Tests` runs each module in isolation with only its own schema reset.
- README shows the three medians. If the per-module host is not at least 3x faster per test than the full host, investigate before merging.

---

## Phase 9 (optional): extraction readiness

Branch: `refactor/09-extraction-readiness`

Goal: prove the seam is real for code and honest about data, without extracting anything.

Changes

1. `docs/extraction-playbook.md`: the steps from chapter 9 applied to Catalog specifically: which Contracts move, which events cross the wire, the dual-write and cutover plan for the `catalog` schema, what callers must change (there should be none at compile time). Includes the two things a network adds that the outbox already models: at-least-once and asynchronous failure.
2. Architecture test: `Catalog.Module` and `Catalog.Contracts` compile with references only to `SharedKernel` and `Orders.Contracts` (for the event it consumes). A second test asserts `Orders.Contracts` has no dependency on anything but `SharedKernel`.
3. A `Catalog.Host` sample project under `samples/` that references `Catalog.Module` and `SharedKernel`, runs the Catalog outbox consumer against the same database, and nothing else. It is not deployed; it exists to make the build fail the day Catalog grows a hidden dependency.

Acceptance

- `samples/Catalog.Host` builds and serves `/api/catalog/health`.
- The playbook names the weeks, not the days.

---

## 4. Order and dependencies

```
0 guardrails  ->  1 postgres  ->  2 split contexts  ->  3 outbox  ->  4 fences
                                                                    ->  5 bulkheads
                                                                    ->  6 reporting (needs 3 for TrackSales)
7 minimal api edges: after 2 (handlers move with the modules)
8 module test host: after 3 and 5
9 extraction readiness: last
```

Phases 4, 5 and 7 can run in parallel branches after phase 3 if you want to, but merge them one at a time.

---

## 5. Kickoff prompt for each phase

Paste this into Claude Code at the repo root, replacing NN:

> Read `docs/upgrade-plan.md` sections 1 through 3 and the section for phase NN. Read every file in `docs/adr/`. Check out main, confirm `dotnet build ModularMonolith.Api.sln` and `dotnet test ModularMonolith.Api.sln` are green, then create the branch named in the phase. Before editing, list the files you intend to add, change and delete for this phase and wait for my confirmation. Then implement the phase step by step, running the full test suite before each commit, adding the ADRs the phase names, and updating README and docs in the same commits as the code. When the acceptance criteria all pass, summarize what changed, what you measured, and anything in the plan that turned out to be wrong.

Commit this file to the repo as `docs/upgrade-plan.md` in phase 0 so the prompt above resolves.

---

## 6. What this plan does not do

- It does not extract a module. Phase 9 rehearses it.
- It does not replace the in-memory Identity stores with a real identity provider. That is a security concern, not a modularity one, and it deserves its own plan.
- It does not add caching tiers, Redis, or a message broker. The outbox dispatcher is in-process on purpose; a broker is what you add when a module leaves the process.
- It does not fix the four removed foreign keys with a saga. Orphans are detected and reported (phase 6), not prevented. That is the branch-two trade, written down.
