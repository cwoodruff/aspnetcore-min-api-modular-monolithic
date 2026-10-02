# ModularMonolith.Api (ASP.NET Core 10 Minimal APIs)

Production-ready modular monolith starter using ASP.NET Core 10 (net10.0) and
Minimal APIs. It demonstrates module composition via a simple IModule
contract, clear boundaries, and integration, service, and architecture tests.

Looking to recreate this solution from scratch? See the step-by-step guide in
docs/Walkthrough.md.

## Solution layout

```
/src
  /ModularMonolith.Api                     (ASP.NET Core 10 Web API host, Minimal APIs)
    Program.cs                             (middleware pipeline and endpoint mapping)
    HostComposition.cs                     (service registration and the explicit module list)
    ModuleComposition.cs                   (startup checks: duplicate routes, unknown policies)
  /Modules
    /Catalog
      /Catalog.Contracts                   (public: what other modules may depend on; empty for now)
      /Catalog.Module                      (internal except the composition class)
        /Domain                            (Artist, Album, Track, Playlist, PlaylistTrack)
        /Data                              (CatalogDbContext, schema "catalog", Migrations/)
        /Models, /Mapping, /Validation     (API models, entity<->model mapping, FluentValidation)
        /Services, /Endpoints              (ArtistService, AlbumService, TrackService, PlaylistService)
    /Orders
      /Orders.Contracts
      /Orders.Module                       (Invoice, InvoiceLine; OrdersDbContext, schema "orders")
    /Administration
      /Administration.Contracts
      /Admin.Module                        (Customer, Employee, Genre, MediaType; AdministrationDbContext,
                                            schema "administration")
    /Reporting/Reporting.Module            (ReportingDbContext, schema "reporting": cross-module views,
                                            integrity findings; read-only role; no Contracts project)
    /Identity
      /Identity.Contracts
      /Identity.Module                     (tokens, users, authorization policies, key management)
  /Shared
    /SharedKernel                          (cross-cutting primitives only; nothing domain-shaped)
      /Caching                             (ICacheFacade, CacheKeyComposer, CompositeCacheFacade)
      /TrafficControl                      (RateLimitPolicyRegistry, PartitionKeys)
      /Persistence                         (ModuleDbContextOptions: UseNpgsql with schema + history table)
/tests
  /ModularMonolith.Api.Tests               (whole-host tests: cross-module flows, pipeline, Identity)
  /ModularMonolith.Module.Tests            (each module on a host of its own: services, handlers, endpoints)
  /ModularMonolith.Architecture.Tests      (Architecture tests: public surface, module boundaries, guardrails)
/samples
  /Catalog.Host                            (Catalog alone in its own process; built, tested, not deployed)
/docs/adr                                  (Architecture decision records)
```

## Module contract

SharedKernel/IModule.cs:

```
public interface IModule
{
    string Name { get; }
    void RegisterServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

Each module exposes public composition entry points only: a public static
<ModuleName>Module with a nested public sealed class Modules : IModule that
registers services and maps endpoints (note the plural "Modules", e.g.,
CatalogModule.Modules). The Identity module also exposes
`IdentityAuthExtensions` for host wiring. Implementation types stay internal by
default, and `tests/ModularMonolith.Architecture.Tests/PublicSurfaceTests.cs`
locks that boundary by asserting each module assembly exports only its intended
composition surface.

## Guardrails

Module ownership is recorded in
[ADR-0001](docs/adr/0001-module-map-and-ownership.md); every later boundary
decision gets its own record in [docs/adr/](docs/adr/README.md). The
following checks keep the code consistent with those records:

- `GuardrailTests` (in `ModularMonolith.Architecture.Tests`):
    - each module grants `InternalsVisibleTo` to at most one assembly, and only
      the test project listed for it in `ArchitectureConstants` (Module.Tests
      for Catalog, Orders, Administration and Reporting; Api.Tests for
      Identity, whose tests replace its services inside the full host);
    - no constructor dependency of a module's internal services resolves to a
      type in another module or the host (the container is built with the same
      `HostComposition.ConfigureServices` the app uses);
    - every authorization policy an endpoint references is registered, and
      is a constant from `Identity.Contracts`
      ([ADR-0011](docs/adr/0011-authorization-names-are-compiled-contracts.md));
      no `RequireAuthorization("...")` literal exists in `src/`;
    - no two endpoints share a route and HTTP method;
    - integration event handlers are registered only as keyed services, and a
      module's key resolves only that module's handlers.
- `ModuleComposition.ValidateEndpoints(app)` runs the route and policy checks
  at startup in the Development and Test environments and refuses to start,
  listing each problem, if either fails.
- `ModuleBoundaryTests` and `SharedKernelDependencyTests` (ArchUnitNET): no
  module depends on another module, no Contracts project or SharedKernel
  depends on a module, `InvoiceFinalized` handlers know Orders only through
  `Orders.Contracts`, and each module's `DbContext` maps only its own entities.
- `FenceTests` ([ADR-0012](docs/adr/0012-shared-kernel-budget.md)): Contracts
  projects hold only interfaces, enums, records and constants, and grant no
  `InternalsVisibleTo`; `SharedKernel` exports at most 30 public types,
  references no FluentValidation, module or Contracts assembly, and uses
  Npgsql only from `SharedKernel.Persistence`.
- `ExtractionReadinessTests`: `Catalog.Module` references only `SharedKernel`
  and the Catalog, Orders and Identity contracts; `Catalog.Contracts` and
  `Orders.Contracts` reference nothing but `SharedKernel`; and
  `samples/Catalog.Host` references only `Catalog.Module` and `SharedKernel`.
  The sample building is the check that Catalog has no hidden dependency
  ([extraction playbook](docs/extraction-playbook.md)).

## Service layer architecture

Each module implements a service layer that encapsulates business logic,
validation, and caching:

### Service responsibilities

- **Input validation** - FluentValidation before persistence operations
- **Cache management** - Cache-aside pattern with tag-based invalidation
- **Data access** - Query the module's own `DbContext` directly; EF Core is the repository
- **Error handling** - Graceful degradation with null/empty returns

### Service pattern example

```csharp
internal sealed class CustomerService(
    AdministrationDbContext db,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<CustomerApiModel> validator) : ICustomerService
{
    // Read with caching
    public async Task<CustomerApiModel?> GetCustomerByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose("administration", "customer", "v1", discriminator: $"by-id:{id}");
        return await cache.GetOrAddAsync<CustomerApiModel?>(key, async _ =>
            await db.Customers.AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new CustomerApiModel { Id = c.Id, FirstName = c.FirstName /* ... */ })
                .SingleOrDefaultAsync(ct),
            new CacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
                Tags = ["administration:customer"]
            }, ct);
    }

    // Write with validation and cache invalidation
    public async Task<CustomerApiModel?> CreateCustomerAsync(CustomerApiModel model, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(model, ct);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);

        var entity = model.ToEntity();
        db.Customers.Add(entity);
        await db.SaveChangesAsync(ct);
        await cache.RemoveByTagAsync("administration:customer", ct);
        return entity.ToApiModel();
    }
}
```

### Services by module

| Module         | Services                                                         |
|----------------|------------------------------------------------------------------|
| Administration | CustomerService, EmployeeService, GenreService, MediaTypeService |
| Catalog          | ArtistService, AlbumService, TrackService, PlaylistService       |
| Orders         | InvoiceService, InvoiceLineService                               |
| Identity       | TokenService, InMemoryUserStore, InMemoryRefreshTokenStore       |

## Validation with FluentValidation

Write endpoints with a body validate it before the handler runs, with
`ValidationFilter<TRequest>` from SharedKernel:

```csharp
group.MapPost("/genres", GenreHandlers.CreateGenre)
    .AddEndpointFilter<ValidationFilter<CreateGenreRequest>>();
```

The filter asks for an `IRequestValidator<TRequest>` (SharedKernel never
references FluentValidation, ADR-0012); Administration registers
`FluentRequestValidator<T>`, which serves its FluentValidation validators. An
invalid body gets the same 400 problem body as a `ValidationException` thrown
by a service (title "Request validation failed.", `errors`, `traceId`); the
service-level validation stays as the fallback.

All input validation uses FluentValidation. Each module keeps its validators,
`internal`, in its own `Validation/` folder:

```csharp
internal sealed class CustomerValidator : AbstractValidator<CustomerApiModel>
{
    public CustomerValidator()
    {
        RuleFor(c => c.FirstName).NotNull().MaximumLength(40);
        RuleFor(c => c.LastName).NotNull().MaximumLength(20);
        RuleFor(c => c.Email).EmailAddress();
        RuleFor(c => c.Phone).Matches(@"\(?\d{3}\)?[-\.]? *\d{3}[-\.]? *[-\.]?\d{4}");
        RuleFor(c => c.PostalCode).Matches(@"^[0-9]{5}(?:-[0-9]{4})?$");
    }
}
```

Each module registers its own validators in `RegisterServices`:

```csharp
services.AddValidatorsFromAssemblyContaining<CustomerValidator>(includeInternalTypes: true);
```

Validation failures and malformed request bodies are centralized in
`src/ModularMonolith.Api/Program.cs`. `ValidationException`,
`BadHttpRequestException`, and `JsonException` now return RFC 7807
`application/problem+json` `400 Bad Request` responses (including `errors`
and `traceId`) instead of surfacing as generic 500s.

See [docs/validation-strategy.md](docs/validation-strategy.md) for complete
documentation.

## Endpoints

Every endpoint maps to a static method on an internal `*Handlers` class in its
module (`group.MapGet("/albums/{id:int}", AlbumHandlers.GetAlbumById)`); the
`*Endpoints` classes only map routes and attach policies. Handlers return typed
results (`Task<Results<Ok<AlbumApiModel>, NotFound>>`), so OpenAPI shows the
exact responses, and they can be unit tested by calling them with a fake
service. The health and data-health handlers return `IResult`: their body is
minimal or detailed depending on the environment.

Endpoint filters: a module group's filters (request metrics) run before an
endpoint's own filters (validation), and filters on one builder run in the
order they were added. `EndpointFilterTests` checks this.


The host discovers all modules and composes their endpoints under conventional
groups:

- GET /api/catalog/health
- GET /api/catalog/data-health
- GET /api/orders/health
- GET /api/orders/data-health
- GET /api/admin/health
- GET /api/admin/data-health
- GET /api/reporting/health
- GET /api/reporting/data-health
- GET /api/identity/health
- GET /api/identity/data-health

In `Development` and `Demo`, health, data-health, and root endpoints return
HTTP 200 with operational metadata such as:

```
{
  "module": "<ModuleName>",
  "status": "Healthy",
  "timestampUtc": "<ISO 8601>",
  "environment": "<ASPNETCORE_ENVIRONMENT>",
  "version": "<AssemblyInformationalVersion>",
  "service": "ModularMonolith.Api"
}
```

Outside `Development`/`Demo`, those same endpoints intentionally return a
minimal payload:

```
{
  "module": "<ModuleName>",
  "status": "Healthy",
  "timestampUtc": "<ISO 8601>"
}
```

For `data-health`, the `status` value is `Data-Healthy` or `Degraded`.
Swagger/OpenAPI and the extra operational metadata are only exposed in
`Development` or `Demo`.

### Invoice finalization and event-fed read models

- `POST /api/orders/invoices/{id}/finalize` (`orders.write`, `tenant.scoped`):
  finalizes a draft invoice and publishes `InvoiceFinalized` through the
  orders outbox. Returns `202 Accepted` with `Location: /api/orders/invoices/{id}`
  and `salesCountersUpdate: "eventual"`; `409` if already finalized.
- `GET /api/catalog/tracks/{id}/sales` (`catalog.read`, `tenant.scoped`): units
  sold, counted by Catalog from those events.
- `GET /api/admin/customers/{id}/purchases` (`role.admin`, `administration.read`,
  `tenant.scoped`): total spent and invoice count, summed by Administration.
- `GET /api/orders/outbox/dead-letters` and
  `POST /api/orders/outbox/dead-letters/{id}/retry` (`role.admin`): outbox
  messages whose delivery failed six times, and a way to send one again.

### Reporting

- `GET /api/reporting/sales-by-genre` and `GET /api/reporting/invoices/{id}/lines`
  (`report.view`, `tenant.scoped`): read through views over the Catalog,
  Orders and Administration schemas, as the read-only `reporting_reader`
  role ([ADR-0014](docs/adr/0014-reporting-read-model-as-views.md)). At most
  4 run at once (`Concurrency:Reporting:MaxConcurrentExpensive`).
- `POST /api/reporting/integrity/run` and `GET /api/reporting/integrity/findings`
  (`role.admin`): runs, and lists, the orphan checks for the four removed
  cross-module foreign keys
  ([ADR-0015](docs/adr/0015-orphan-detection.md)). The job also runs daily at
  `Reporting:Integrity:RunAtUtc` (default 02:00 UTC). It never repairs data.

The two read models are eventually consistent and count only invoices
finalized through the endpoint; the seeded invoices start as `Draft`. See
[How modules talk to each other](#how-modules-talk-to-each-other).

## Run it

The app needs PostgreSQL 17. Start the local database, then run the API:

```
docker compose up -d
dotnet run --project src/ModularMonolith.Api
```

In Development the API applies migrations and loads the Chinook seed
(`data/chinook-postgres-seed.sql`) on first start, then serves
http://localhost:5043. Check it with
`curl http://localhost:5043/api/catalog/data-health`, which should report
`"connected": true`. `docker compose down -v` deletes the database volume, and
the next start seeds it again.

## Build, run, and test

- Build: `dotnet build ModularMonolith.Api.sln`
- Run: see [Run it](#run-it)
- Swagger UI (Development/Demo only): http://localhost:5043/swagger (or the
  https port from launch settings)
- Tests: `dotnet test ModularMonolith.Api.sln` (needs Docker; the integration
  tests start their own PostgreSQL container with Testcontainers, so the compose
  database does not have to be running)
    - Solution-level runs include `ModularMonolith.Api.Tests`,
      `ModularMonolith.Module.Tests`, and
      `ModularMonolith.Architecture.Tests`.
    - `tests/timing.sh` (or `tests/timing.ps1`) times each project; see
      [Test cost](#test-cost).
    - Test coverage examples include root and per-module health/data-health
      gating, validation `ProblemDetails`, authenticated flows (e.g., obtaining
      a JWT and calling protected Catalog endpoints), admin authorization, and
      module public-surface enforcement.
    - The host exposes a public partial Program class to support
      Microsoft.AspNetCore.Mvc.Testing’s WebApplicationFactory.
- CI: `.github/workflows/dotnet.yml` builds and tests the solution on every
  push to `main` and every pull request.

### Example curl commands

```
curl http://localhost:5043/api/catalog/health
curl http://localhost:5043/api/orders/health
curl http://localhost:5043/api/admin/health
curl http://localhost:5043/api/reporting/health
curl http://localhost:5043/api/identity/health
curl http://localhost:5043/
```

## Docker

Build and run the container:

```
docker build -t modular-monolith-api .
docker run -p 8080:8080 \
  -e ConnectionStrings__AppDatabase="Host=host.docker.internal;Port=5432;Database=chinook;Username=chinook;Password=chinook" \
  modular-monolith-api
```

- The container needs a connection string. Outside Development it does not
  migrate or seed, so point it at a database that already has the schema and
  data, such as the compose database after one `dotnet run` in Development.

- Dev ports vs Docker ports: When running locally via launchSettings.json the
  app listens on http://localhost:5043 and https://localhost:7043. In the
  container, ASPNETCORE_URLS is set to http://+:8080, so
  expose/browse http://localhost:8080.

If you do run the container in `Development` or `Demo` after aligning the
Dockerfile, browse http://localhost:8080/swagger.

## Notes

### Rate limiting

- Each module has its own fixed-window policy, applied once on its route group
  in `MapEndpoints`: `catalog:api`, `orders:api`, `admin:api`, `identity:api`,
  `reporting:api`. `global:public-anon` covers only the root endpoint (`GET /`).
  Requests are partitioned by `PartitionKeys.FromRequest` (client id, tenant,
  subject, then IP).
- Limits come from `RateLimiting:Policies:<name>` (`PermitLimit`,
  `WindowSeconds`, `QueueLimit`); the default is 60 requests per 60 seconds
  with no queue. For example, to give Catalog more room:

```
"RateLimiting": {
  "Policies": {
    "catalog:api": { "PermitLimit": 300, "WindowSeconds": 60 }
  }
}
```

- Do not add `RequireRateLimiting` to individual endpoints; the architecture
  tests expect every module endpoint to carry exactly its module's policy. See
  [Failure domain](#failure-domain).

### Logging and observability

- Uses built-in ASP.NET Core logging by default.
- Metrics: each module has a `System.Diagnostics.Metrics` meter named
  `ModularMonolith.<Module>` (for example `ModularMonolith.Catalog`). Every
  measurement is tagged `module`. Instruments: `modmono.http.requests`
  (tagged `status_code`), `modmono.cache.hits`, `modmono.cache.misses`,
  `modmono.outbox.published`, `modmono.outbox.dispatched`,
  `modmono.outbox.retried`, `modmono.outbox.dead_lettered`, and
  `modmono.work_queue.depth`. No exporter is wired; add OpenTelemetry or
  `dotnet-counters monitor --counters ModularMonolith.Catalog` to read them.
- Health: `GET /healthz` reports each module's database check, tagged with the
  module name.

### Using Swagger & OpenAPI

#### Identity endpoints summary

- POST /api/identity/login — Issues an access token and refresh token for valid
  credentials. AllowAnonymous.
- POST /api/identity/refresh — Exchanges a valid refresh token for a new access
  token. AllowAnonymous.
- POST /api/identity/logout — Revokes a refresh token for the current user.
  Requires Authorization.
- GET /api/identity/userinfo — Returns basic claims (sub, name, email, roles,
  permissions). Requires Authorization.
- GET /api/identity/.well-known/jwks.json — Exposes the JWKS document for the
  signing key. AllowAnonymous.
- Swagger UI is enabled at `/swagger` only when the app runs in the
  `Development` or `Demo` environment.
- OpenAPI documents: `/swagger/v1/swagger.json` (all modules) and one per
  module, `/swagger/{catalog|orders|admin|identity|reporting}/swagger.json`,
  each holding only the endpoints tagged with that module. Swagger UI lists
  them all. Response types come from the handlers' typed results.
- JWT Bearer auth is integrated into Swagger:
    - Click the "Authorize" button in Swagger UI and paste the access token
      only (do NOT include the `Bearer ` prefix). Swagger will add it
      automatically.
    - In-memory login is only enabled in the `Development` or `Demo`
      environment when `Identity:InMemoryUsers` is configured.
- Once authorized, protected endpoints (e.g., Catalog Albums) can be executed
  directly from Swagger UI.

#### Development-only in-memory users

The app no longer ships with baked-in usernames/passwords. Configure your own
development-only users with user secrets or environment variables before
calling `POST /api/identity/login`.

> [!IMPORTANT]
> In-memory login only works when the app runs in the `Development` or `Demo`
> environment. If you launch the API outside the default `dotnet run` launch
> profile, set `ASPNETCORE_ENVIRONMENT=Development` (or `Demo`) yourself.
> Also note that each `Identity:InMemoryUsers:<index>` entry must include
> `Username`, `Password`, and `UserId`; entries missing any of those fields are
> ignored.

##### User account model

The `Identity` configuration section binds to the `InMemoryUserStoreOptions`
options model in the Identity module, and its `InMemoryUsers` array contains
`InMemoryUserRecord` entries. Each array item becomes one login account if it
has `Username`, `Password`, and `UserId`; entries missing any of those three
values are ignored. The configured password is compared as-is by the in-memory
store, so treat it as a dev/demo-only secret and do not reuse production
credentials.

| Field | Required | How the current code uses it |
| --- | --- | --- |
| `Username` | Yes | Login identifier for `POST /api/identity/login`. The in-memory store trims it, stores it case-insensitively, and uses it as the lookup key. |
| `Password` | Yes | Password for `POST /api/identity/login`. The in-memory store compares the configured value directly against the submitted password. |
| `UserId` | Yes | Stable subject identifier. Issued into the JWT `sub` claim and used by refresh/logout flows (`POST /api/identity/refresh`, `POST /api/identity/logout`). |
| `DisplayName` | No | If set, issued into the token as the name claim and returned by `GET /api/identity/userinfo` as `name`. If omitted, the store falls back to the trimmed `Username`. |
| `Roles` | No | Each value becomes a role claim in the JWT. The built-in `role.admin` policy requires the `Admin` role. |
| `Permissions` | No | Each value becomes a `permissions` claim in the JWT. Authorization policies registered in `PolicyRegistry` use these values directly as policy names (for example `catalog.read`, `catalog.write`, `orders.read`, `orders.write`, `admin.users.manage`, `administration.read`, `administration.write`, `report.view`). |
| `Email` | No | If set, issued into the token as the email claim and returned by `GET /api/identity/userinfo` as `email`. |
| `Tenant` | No | If set, issued into the token as the `tenant` claim. The `tenant.scoped` policy uses that claim to enforce tenant matching. |

##### Authorization mapping for roles, permissions, and tenant

- **Roles**
    - Roles are emitted as standard role claims.
    - The built-in role convenience policy is `role.admin`, which requires the
      `Admin` role.
    - The Admin module endpoints now require `role.admin` in addition to the
      appropriate `administration.*` permission.
- **Permissions**
    - `PolicyRegistry` registers permission policies whose names exactly match
      the claim values in `Permissions`.
    - A user only satisfies one of those policies when the JWT contains a
      matching `permissions` claim.
- **Tenant**
    - `tenant.scoped` requires an authenticated user plus the custom
      `TenantAuthorizationHandler`.
    - The handler reads the JWT `tenant` claim and compares it to the request
      tenant resolved in this order: route value `tenant`, route value
      `tenantId`, then header `X-Tenant-Id`.
    - If no route/header tenant is supplied, the handler implicitly scopes the
      request to the user's own tenant claim.
    - If the token has no `tenant` claim, `tenant.scoped` authorization fails.
    - There is no hard-coded tenant value for admin users; any tenant string is
      valid as long as the token's `tenant` claim and any supplied
      `X-Tenant-Id` header match exactly.

##### Admin module access requirements

- `GET /api/admin/customers`, `GET /api/admin/employees`,
  `GET /api/admin/genres`, and `GET /api/admin/media-types` currently require:
    - `Roles` to include `Admin`
    - `Permissions` to include `administration.read`
    - `Tenant` to be set so the JWT carries a `tenant` claim
- `POST`, `PUT`, and `DELETE` genre endpoints currently require:
    - `Roles` to include `Admin`
    - `Permissions` to include `administration.write`
    - `Tenant` to be set
- `Admin` does not replace `administration.read` or `administration.write`; the
  current code requires both the role and the matching permission.
- Keep every field for one user on the same `Identity:InMemoryUsers:<index>`
  entry. Splitting one account across multiple indexes produces one valid user
  with partial claims and one ignored incomplete record.
- On startup in Development/Demo, the app now logs the effective username,
  userId, roles, permissions, tenant, and password length loaded from each valid
  in-memory user entry (the password value itself is never logged). If login
  succeeds but authorization is wrong, check that summary first.
- Any entry that is missing `Username`, `Password`, or `UserId` is reported
  separately as a warning naming its index and missing field(s), so a split or
  half-finished account is visible at startup rather than at login time. See
  [Troubleshooting 401 responses](#troubleshooting-401-responses-from-apiidentitylogin).

##### Full user-secrets example

The following creates two development/demo accounts. The first can read Catalog
data in `tenant-1`; the second is an admin-oriented account in `tenant-admin`.

```bash
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Username" "demo"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Password" "<choose-a-strong-password>"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:UserId" "user-1"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:DisplayName" "Demo User"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Roles:0" "User"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Permissions:0" "catalog.read"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Email" "demo@example.com"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Tenant" "tenant-1"

dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:1:Username" "admin-demo"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:1:Password" "<choose-another-strong-password>"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:1:UserId" "user-2"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:1:DisplayName" "Admin Demo"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:1:Roles:0" "Admin"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:1:Permissions:0" "admin.users.manage"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:1:Permissions:1" "administration.read"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:1:Permissions:2" "administration.write"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:1:Email" "admin-demo@example.com"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:1:Tenant" "tenant-admin"
```

If you only need one admin-capable account, keep the entire user on a single
index, for example:

```bash
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Username" "admin-demo"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Password" "<choose-a-strong-password>"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:UserId" "user-admin-1"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:DisplayName" "Admin Demo"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Roles:0" "Admin"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Permissions:0" "administration.read"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Permissions:1" "administration.write"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Permissions:2" "admin.users.manage"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Email" "admin-demo@example.com"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Tenant" "tenant-admin"
```

If you are repurposing an existing index, update or remove every stale key
under that same index before testing again.

To add more accounts, increment the array index (`0`, `1`, `2`, ...). Nested
arrays use the same pattern for multi-value fields such as roles and
permissions, for example:

```bash
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:2:Roles:0" "User"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:2:Permissions:0" "orders.read"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:2:Permissions:1" "orders.write"
```

##### Troubleshooting 401 responses from `/api/identity/login`

A 401 from the login endpoint means the in-memory store found no user whose
username and password both match the request. The store compares the configured
password to the submitted password exactly, so any stray character stored in the
secret causes a mismatch.

Check what is actually stored:

```bash
dotnet user-secrets --project src/ModularMonolith.Api list
```

A common cause is a shell paste accident, where the password and the next
command end up on the same line and are stored as a single value:

```text
Identity:InMemoryUsers:0:Password = hunter2curl -s localhost:5043/api/catalog/data-health
```

The startup log makes this visible without printing the secret. In
Development/Demo each loaded entry logs as:

```text
Effective Identity:InMemoryUsers:0 => Username='demo', UserId='user-1', Roles='User', Permissions='catalog.read', Tenant='tenant-1', PasswordLength=8.
```

If `PasswordLength` does not match the password you are typing, re-set the
secret and quote the value so nothing else can join the line:

```bash
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Password" '<choose-a-strong-password>'
```

Other things to confirm when login returns 401:

- The app is running with `ASPNETCORE_ENVIRONMENT` set to `Development` or
  `Demo`. Outside those environments the in-memory store is replaced by a
  disabled store that rejects every credential, and the startup log warns that
  the configured entries are ignored.
- The entry is complete. `Username`, `Password`, and `UserId` are all required.
  An entry missing any of them is skipped, and startup logs one warning per bad
  entry naming the index and every missing field:

  ```text
  Ignoring Identity:InMemoryUsers:1 because it is missing required field(s): Password. Keep Username, Password, UserId, Roles, Permissions, Email, and Tenant for one account on the same array index.
  ```

  These per-index warnings are emitted whenever the app starts in
  Development/Demo, including when no entry is usable at all, so a first-time
  setup always shows which index needs fixing. They are followed by a summary
  line: `Loaded 2 in-memory login users and ignored 1 incomplete ...` when at
  least one entry loaded, or `none of the N configured Identity:InMemoryUsers
  entries are usable` when none did.
- The app was restarted after the secrets changed. User secrets are read at
  startup only.

##### Security model

- These accounts exist only when the app runs in the `Development` or `Demo`
  environment **and** `Identity:InMemoryUsers` is configured.
- Outside `Development`/`Demo`, the in-memory user store is replaced with a
  disabled implementation, so the demo login path does not provide accounts by
  default.
- This store is for local development/demo scenarios only. It is not a
  production identity system.
- For production, integrate a real identity provider and signing key source;
  do not treat `Identity:InMemoryUsers` as production-ready authentication.

#### JWT signing key configuration

- `Development`/`Demo` now persists the RSA signing key to
  `src/ModularMonolith.Api/data/identity/dev-jwt-signing-key.json` by default
  (configured via `Jwt:DevelopmentKeyPath` in
  `src/ModularMonolith.Api/appsettings.Development.json`).
- Reuse that file or point `Jwt:DevelopmentKeyPath` at another persistent path
  if you need tokens to survive local restarts.
- Outside `Development`/`Demo`, the app refuses to fall back to the dev key and
  requires an external signing key provider.
- Azure Key Vault is supported via:

```bash
export Jwt__KeyProvider=KeyVault
export Jwt__KeyVaultVaultUri=https://<your-vault>.vault.azure.net/
export Jwt__KeyVaultKeyName=<your-rsa-signing-key-name>
```

The configured RSA key is used for token signing and its public key is exposed
through `GET /api/identity/.well-known/jwks.json`.

#### JWT for Catalog endpoints

- Where JWT is processed in code:
    - Global authentication/authorization is configured in
      `src/Modules/Identity/Identity.Module/Extensions/IdentityAuthExtensions.cs`.
    - Catalog endpoints opt-in to authorization using `.RequireAuthorization(...)`
      in each endpoint mapping.
    - Example: `GET /api/catalog/albums/{id}` is protected by `catalog.read` and
      `tenant.scoped` policies in
      `src/Modules/Catalog/Catalog.Module/Endpoints/AlbumEndpoints.cs`.
- How to use JWT in Swagger for Catalog endpoints:
    1) Call `POST /api/identity/login` to receive an `access_token`.
    2) In Swagger UI, click the Authorize button and enter: `<access_token>`.
    3) For tenant-scoped endpoints, set a tenant hint (current routes do not
       include a {tenant} segment by default):
        - Preferred: Header `X-Tenant-Id: <your-tenant>`.
        - Optional: A route value may be used if a module defines such a
          template in the future (e.g., `/api/catalog/{tenant}/albums/{id}` —
          hypothetical, not defined by default).
    4) Invoke Catalog endpoints. You will see:
        - 200 OK when the token includes `permissions: ["catalog.read"]` and
          tenant scope matches.
        - 403 Forbidden if missing permission or tenant mismatch.
        - 401 Unauthorized if no/invalid token is provided.

### New: Catalog Albums Endpoint (GET /api/catalog/albums/{id})

- Path: GET /api/catalog/albums/{id}
- Module: Catalog
- Authorization: Requires a valid JWT with the `catalog.read` permission and the
  `tenant.scoped` policy (the token's `tenant` claim must match the request's
  tenant hint when one is supplied).
- Caching: Uses Catalog's own cache (an ICacheFacade keyed "Catalog") with a
  namespaced key composed by CacheKeyComposer.
    - Key shape example: {env}:{app}:catalog:album:v1::::by-id:{id}
    - Default TTL: 20 minutes (with jitter to avoid stampede). Adjust via
      Caching:* configuration if needed.
- Data source: PostgreSQL (`catalog` schema) via CatalogDbContext; includes Artist info.

How it works

- On request, the endpoint composes a cache key (module=catalog, entity=album,
  version=v1, discriminator=by-id:{id}).
- It calls cache.GetOrAddAsync(key, factory) where factory queries the database
  if the cache is missed.
- Non-existent IDs return 404 (not cached). Existing albums are cached for
  faster subsequent reads.
- The endpoint is protected; include Authorization: Bearer <token> header.
  Obtain a token via POST /api/identity/login with your configured development
  credentials.

Example

1) Get token

```
curl -s -X POST http://localhost:5043/api/identity/login \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"$IDENTITY_USERNAME\",\"password\":\"$IDENTITY_PASSWORD\"}"
```

Response contains access_token.

2) Fetch album 1 using token

```
TOKEN="<paste-access-token>"
curl -H "Authorization: Bearer $TOKEN" http://localhost:5043/api/catalog/albums/1
```

Notes

- To change caching behavior, use the Caching:* configuration section. Each
  module's L1 cache has its own size limit (Caching:Modules:<Module>:SizeLimit);
  L2 (e.g., Redis) is optional and shared.
- If you later add write endpoints that mutate album data, evict the
  corresponding cache key(s) or bump the version prefix (v1→v2) to ensure
  readers don’t see stale data.

- Swagger/OpenAPI is enabled in `Development`/`Demo` with tags per module.
- CORS policy named "Default" allows common localhost dev origins: http(s):
  //localhost:3000, 4200, 5173.
- ProblemDetails middleware is enabled via UseExceptionHandler and
  AddProblemDetails, with centralized 400 responses for validation and malformed
  request bodies.
- No cross-module references; only the host references the modules and
  SharedKernel.

## Data and persistence

- Engine: PostgreSQL 17 ([ADR-0002](docs/adr/0002-database-engine.md)).
- One `DbContext` per module ([ADR-0003](docs/adr/0003-one-dbcontext-per-module.md)):
  `CatalogDbContext`, `OrdersDbContext` and `AdministrationDbContext`. Each maps
  only its own module's entities, uses its own schema (`catalog`, `orders`,
  `administration`; see [ADR-0001](docs/adr/0001-module-map-and-ownership.md))
  and keeps its migration history in `<schema>.__EFMigrationsHistory`. Each
  module registers its context in `RegisterServices` with
  `ModuleDbContextOptions.AddModuleDbContext<T>(schema)`; the host registers
  none. Contexts are pooled (`AddDbContextPool`, 128 per module).
- No navigation property or foreign key crosses a module line. `Invoice.CustomerId`,
  `InvoiceLine.TrackId`, `Track.GenreId` and `Track.MediaTypeId` are plain,
  indexed ids; ADR-0004 to ADR-0007 record what that means for each.
- Migrations live in each module's `Data/Migrations`. The `dotnet-ef` tool is
  pinned in `dotnet-tools.json`; run `dotnet tool restore` once, then for
  example:
  ```
  dotnet ef migrations list --project src/Modules/Catalog/Catalog.Module --context CatalogDbContext
  dotnet ef migrations add <Name> --project src/Modules/Orders/Orders.Module --context OrdersDbContext --output-dir Data/Migrations
  ```
  Each module's design-time factory reads `ConnectionStrings__AppDatabase` and
  falls back to the compose database.
- Seed: `data/chinook-postgres-seed.sql`. In Development and Test the host's
  `DbSeeder` migrates Administration, Catalog, then Orders, and loads the seed
  when `catalog."Track"` is empty. Turn it off with
  `Database:MigrateAndSeedOnStartup=false`. Other environments apply
  migrations as a deployment step.
- Reporting connects with its own login, `ConnectionStrings:Reporting`, in the
  read-only `reporting_reader` role that its migration creates and grants
  (`SELECT` on the other three schemas, writes only to
  `reporting.IntegrityFinding`). `appsettings.Development.json` points it at
  the `reporting` login, which `docker/postgres-init` creates when the compose
  volume is first made. On an older volume, create it once:
  `docker exec -i modular-monolith-postgres psql -U chinook -d chinook < docker/postgres-init/01-reporting-login.sql`.
  Other environments must provide a login in that role.
- Upgrading a local database from phase 1: the history tables moved into the
  module schemas, so a phase 1 compose volume cannot be migrated in place. Run
  `docker compose down -v` once; the next `dotnet run` recreates and seeds it.

### Connection string configuration

- Set the PostgreSQL connection via appsettings (ConnectionStrings:
  AppDatabase), user secrets, or the environment variable
  ConnectionStrings__AppDatabase. `appsettings.Development.json` points at the
  compose database; `appsettings.json` has no default, and the app refuses to
  create a DbContext without one.

### Caching configuration

- Per module: each module calls `AddModuleCache(name, sizeLimit)` in
  `RegisterServices` and gets its own `MemoryCache` (every entry counts 1 toward
  the limit) and its own `ICacheFacade`, which its services take with
  `[FromKeyedServices(<Module>.ModuleName)]`. Size limits:
  `Caching:Modules:<Module>:SizeLimit` (Catalog 1000, Orders 1000,
  Administration 500 by default).
- Tier: Caching:Tier can be "L1" (default, in-memory only) or "L1L2" (adds an
  optional distributed cache, shared by every module, if available/configured).
- Provider: Caching:Provider can be "InMemory" by default; use your own
  registration for Redis/others and the facade will detect IDistributedCache.
- Defaults: CacheEntryOptions support AbsoluteExpirationRelativeToNow,
  SlidingExpiration, Jitter (±10% by default) to avoid stampedes.
- Cache key composition: Keys include environment and service name components,
  derived from configuration keys ASPNETCORE_ENVIRONMENT/DOTNET_ENVIRONMENT and
  ServiceName (defaults to "mmapi"). See
  SharedKernel/Caching/CacheKeyComposer.cs.

### Service-level caching

All module services implement caching using the cache-aside pattern:

```csharp
// Cache tags for bulk invalidation
private static readonly string[] CustomerTags = ["administration:customer", "administration:customer:by-id"];

// Read with caching
var key = keys.Compose("administration", "customer", "v1", discriminator: $"by-id:{id}");
return await cache.GetOrAddAsync<CustomerApiModel?>(key, async _ => await LoadByIdAsync(id, ct),
    new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20), Tags = CustomerTags }, ct);

// Write with cache invalidation
await cache.RemoveByTagAsync(CustomerTags[0], ct);
```

See [docs/caching-strategy.md](docs/caching-strategy.md) for complete
documentation including service-level patterns.

### How modules access data

A module reads and writes only its own tables, through its own `DbContext`,
from its services. There is no repository layer: EF Core is the repository,
reads use `AsNoTracking`, and query projections live as private `Load*Async`
methods next to the service code that uses them. To use another module's data,
a module goes through that module's `*.Contracts` project, never a join. The architecture tests enforce both: no
module depends on another module's assembly, and each context's model contains
only its own module's entity types.

## How modules talk to each other

Modules never call into each other or share a transaction. When one module's
change matters to another, it publishes an integration event through its own
outbox, and the others react in their own transactions. See
[ADR-0008](docs/adr/0008-integration-events-and-outbox.md) for the full
semantics; in short:

- **Publish with the write.** `IEventPublisher.PublishAsync(event, db, ct)` adds
  a row to the publisher's outbox table on the same context, so the event
  commits with the business change or not at all. Event types live in the
  publisher's `*.Contracts` project (`Orders.Contracts/Events/InvoiceFinalized`);
  consumers reference only that.
- **Deliver at least once.** Each publishing module runs an `OutboxDispatcher`
  hosted service that polls its outbox (`FOR UPDATE SKIP LOCKED`, 50 at a time)
  and hands each event to every `IIntegrationEventHandler<T>` registered by
  other modules.
- **Apply once.** Each handler runs inside `InboxGuard` on its own module's
  context: the `(EventId, HandlerName)` row in that module's inbox is written
  with the handler's changes, so a redelivered event is skipped.
- **Retry, then ask a person.** Failed deliveries are retried after 1 s, 5 s,
  30 s, 2 min and 10 min; the sixth failure dead-letters the message, which
  then waits for `POST /api/orders/outbox/dead-letters/{id}/retry`.
- **Order only within one outbox**, in write order, and only until a message
  fails. Nothing is ordered across modules.

Today Orders publishes `InvoiceFinalized`; Catalog keeps `TrackSales`
([ADR-0009](docs/adr/0009-orders-to-catalog-track-sales.md)) and
Administration keeps `CustomerPurchaseSummary`
([ADR-0010](docs/adr/0010-orders-to-administration-purchase-summary.md)).

Configuration: `Outbox:Enabled` (default `true`) and
`Outbox:PollIntervalSeconds` (default `1`).

## Native AOT readiness

SharedKernel and the four Contracts projects build with `IsAotCompatible`, so
the trimming and AOT analyzers run on every build and any warning fails it.
They use the configuration-binding source generator, carry the
`DynamicallyAccessedMembers` annotations their generic helpers need, dispatch
integration events through typed delegates instead of reflection, and
serialize outbox payloads and L2 cache entries through `JsonTypeInfo` from the
JSON options registered under `ModuleJson.OptionsKey`. This host registers
reflection-based options (`AddReflectionJsonSerialization()`); an AOT host
would register the modules' source-generated `JsonSerializerContext`s there
instead.

The modules and the host are not AOT-compatible yet, and are not analysed:
Swashbuckle and FluentValidation rely on reflection, and EF Core's runtime
model building and query translation would need compiled models and
precompiled queries. Publishing is unchanged (no `PublishAot`).

## Failure domain

All modules run in one process. Phase 5 gives each module its own share of what
can be split in-process ([ADR-0013](docs/adr/0013-per-module-bulkheads.md)):

| A module can isolate in-process | How |
|---|---|
| Its request budget | Its own rate-limit policy on its route group: a burst against Catalog returns 429 from Catalog only |
| Its cache | Its own `MemoryCache` with a size limit: filling it never evicts another module's entries |
| Its background work | Its own bounded `ModuleWorkQueue`: a backlog makes producers wait instead of growing memory |
| Its expensive operations | Its own `ModuleGate` concurrency cap |
| Its visibility | Its own meter (`module` tag on every measurement) and its own `/healthz` entry |
| Its data and failures in event handling | Its own schema, DbContext, inbox and retries (ADR-0003, ADR-0008) |

| A module cannot isolate in-process | Why |
|---|---|
| Memory and GC pauses | One heap; a gen-2 collection stops every module |
| The thread pool | One pool; blocking or CPU-heavy code anywhere starves everyone |
| Crashes | An unhandled background exception, stack overflow or out-of-memory ends the process |
| The database server | One PostgreSQL server: connections, CPU and locks are shared |
| Deploys | One build, one release, one restart, one rollback |

When one of the second list is the problem, the answer is extraction, not
another in-process limit. [docs/extraction-playbook.md](docs/extraction-playbook.md)
walks through it for Catalog, and `samples/Catalog.Host` runs Catalog on its
own: `dotnet run --project samples/Catalog.Host` serves it on
http://localhost:5143 against the compose database, accepting tokens from the
monolith on port 5043, and reads `InvoiceFinalized` from Orders' outbox by
cursor without taking rows from Orders' dispatcher
([ADR-0017](docs/adr/0017-catalog-host-reads-orders-outbox-by-cursor.md)).

## Documentation

Detailed documentation is available in the `/docs` folder:

### Architecture & Implementation

- [Decision records](docs/adr/README.md) - ADRs, starting with the module map
  and entity ownership; [ADR-0008](docs/adr/0008-integration-events-and-outbox.md)
  covers integration events and the outbox
- [Upgrade plan](docs/upgrade-plan.md) - Phased plan for the modular monolith
  fixes; each phase is one branch and one PR
- [Extraction playbook](docs/extraction-playbook.md) - What taking Catalog out
  of the process would involve, week by week
- [Services Architecture](docs/services-architecture.md) - Service layer
  patterns, caching integration, and validation
- [Validation Strategy](docs/validation-strategy.md) - FluentValidation
  implementation and patterns
- [Caching Strategy](docs/caching-strategy.md) - Multi-tier caching with
  tag-based invalidation
- [EF Core Plan](docs/EFCore-Plan.md) - Per-module DbContexts, schemas,
  migrations and seeding
- [Walkthrough](docs/Walkthrough.md) - Step-by-step guide to recreate the
  solution

### Security

- [Authentication & Authorization](docs/authn-authz-plan.md) - JWT bearer
  authentication and policy-based authorization
- [OWASP Threats & Mitigations](docs/owasp-top-threats-and-mitigations.md) -
  Security best practices
- [Secure Headers Plan](docs/secure-headers-plan.md) - HTTP security headers
  configuration
- [HTTPS Enforcement](docs/https-enforcement-plan.md) - TLS configuration guide

### Traffic Control

- [Rate Limiting Plan](docs/rate-limiting-plan.md) - Centralized rate limiting
  and throttling

## Test Coverage

The solution includes three test projects:

- `tests/ModularMonolith.Module.Tests/` - each module on a host of its own
  ([ADR-0016](docs/adr/0016-a-test-host-per-module.md)).
  `ModuleTestHost<TModule>` registers SharedKernel's services and the one
  module, against a database cloned from a template holding only that
  module's schema (Reporting also gets the three schemas its views read).
  A header-based test scheme stands in for Identity, published events are
  recorded, and `DeliverAsync` feeds the module's event handlers through its
  inbox. Services, handlers and endpoints are tested per module; the outbox
  dispatcher is tested once, in `Kernel/`, with test contexts.
  `Samples/CatalogHostTests` boots `samples/Catalog.Host` and checks its
  outbox feed
- `tests/ModularMonolith.Api.Tests/` - the whole host through
  `WebApplicationFactory`, for what needs more than one module: an invoice
  finalized in Orders reaching Catalog and Administration, the
  authorization pipeline, rate limits across modules, health, security
  headers, error handling, OpenAPI, seed integrity, and Identity. Each host
  gets its own clone of a seeded template built through `HostComposition`
  and `DbSeeder` (`PostgresFixture`, `ApiFactory`)
- `tests/ModularMonolith.Architecture.Tests/` - architecture tests such as
  `PublicSurfaceTests` and `GuardrailTests`

Both database projects start one PostgreSQL container per run with
Testcontainers; test classes run in parallel on their own database clones.

Representative coverage areas include:

| Test Category      | Description                                       |
|--------------------|---------------------------------------------------|
| Health endpoints   | Module health and data-health endpoint tests      |
| Identity endpoints | Login, refresh, logout, userinfo, JWKS tests      |
| Write operations   | POST/PUT/DELETE endpoint tests with authorization |
| Rate limiting      | 429 response behavior tests                       |
| Caching behavior   | Cache consistency and stampede prevention tests   |
| Error scenarios    | Invalid JSON, validation errors, edge cases       |
| Architecture       | Public surface, boundaries, DI, routes, policies  |

Run tests with coverage:

```bash
dotnet test ModularMonolith.Api.sln --collect:"XPlat Code Coverage"
```
Use the coverage report from that command as the current source of truth.

### Test cost

Median wall clock of three runs per project (`tests/timing.sh`, Release build,
container start included), on an Apple M5 Pro (18 cores, 24 GB), macOS 27.2,
Docker 29.8, .NET SDK 10.0.401:

| Project            | Tests | Median (s) | Per test (ms) |
|--------------------|------:|-----------:|--------------:|
| Architecture.Tests |    76 |       1.61 |            21 |
| Module.Tests       |   166 |       5.72 |            34 |
| Api.Tests          |   116 |      15.78 |           136 |

A test on a module's own host costs about a quarter of one on the full host.
Before phase 8 the same machine measured Services.Tests at 83 tests in
5.81 s (70 ms per test, all three schemas migrated into one database) and
Api.Tests at 201 tests in 27.82 s (138 ms per test): moving the single-module
endpoint tests cut the two database projects from 33.6 s to 21.5 s, for 282
tests against 284 (several old ones are now one theory each, with exact
statuses where they accepted any of several).

Phase 1 moved the tests from a copied SQLite file to PostgreSQL in a
Testcontainers container. Api.Tests on the same machine, median of three
runs (`dotnet test` reported duration):

| Api.Tests            | SQLite, before phase 1 | PostgreSQL, phase 1 |
|----------------------|-----------------------:|--------------------:|
| Full run             |                 13.1 s |                20 s |
| One test on its own  |                 0.89 s |                ~3 s |
| Median per test      |                 524 ms |              786 ms |

The single-test cost is the container start and building the migrated,
seeded template database, paid once per run. The per-test rise is each host
getting its own clone and talking to a real server instead of a local
file.
