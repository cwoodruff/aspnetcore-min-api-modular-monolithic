# ModularMonolith.Api (ASP.NET Core 10 Minimal APIs)

Production-ready modular monolith starter using ASP.NET Core 10 (net10.0) and
Minimal APIs. It demonstrates module
composition via a simple IModule contract, clear boundaries, and integration
tests.

Looking to recreate this solution from scratch? See the step-by-step guide in
docs/Walkthrough.md.

## Solution layout

```
/src
  /ModularMonolith.Api                     (ASP.NET Core 10 Web API host, Minimal APIs)
  /Modules
    /Music/Music.Module                    (Class Library)
      /Services                            (IArtistService, IAlbumService, ITrackService, IPlaylistService)
      /Endpoints                           (ArtistEndpoints, AlbumEndpoints, TrackEndpoints, etc.)
    /Orders/Orders.Module                  (Class Library)
      /Services                            (IInvoiceService, IInvoiceLineService)
      /Endpoints                           (InvoiceEndpoints, InvoiceLineEndpoints)
    /Administration/Admin.Module           (Class Library)
      /Services                            (ICustomerService, IEmployeeService, IGenreService, IMediaTypeService)
      /Endpoints                           (CustomerEndpoints, EmployeeEndpoints, GenreEndpoints, etc.)
    /Reporting/Reporting.Module            (Class Library - health endpoints only)
    /Identity/Identity.Module              (Class Library)
      /Services                            (TokenService, UserStore, RefreshTokenStore)
      /Endpoints                           (AuthEndpoints)
      /Authorization                       (PolicyRegistry, TenantAuthorizationHandler)
      /KeyManagement                       (DevKeyMaterialService)
  /Shared
    /SharedKernel                          (Class Library for cross-cutting primitives)
      /Caching                             (ICacheFacade, CacheKeyComposer, CompositeCacheFacade)
      /TrafficControl                      (RateLimitPolicyRegistry, PartitionKeys)
    /SharedKernel.Persistence              (Class Library for EF Core, entities, validation)
      /Entities                            (Album, Artist, Customer, Employee, Genre, etc.)
      /ApiModels                           (AlbumApiModel, ArtistApiModel, etc.)
      /Repositories                        (IAlbumRepository, IArtistRepository, etc.)
      /Validation                          (FluentValidation validators)
    /SharedKernel.DataSQLite               (Class Library for SQLite repository implementations)
      /Repositories                        (AlbumRepository, ArtistRepository, BaseRepository<T>)
/tests
  /ModularMonolith.Api.Tests               (xUnit integration tests using WebApplicationFactory)
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

Each module exposes a public static <ModuleName>Module with a nested public
sealed class Modules : IModule that registers services and maps endpoints (note
the plural "Modules", e.g., MusicModule.Modules). Only the IModule is public
outside the module; all other types should remain internal by default.

## Service layer architecture

Each module implements a service layer that encapsulates business logic,
validation, and caching:

### Service responsibilities

- **Input validation** - FluentValidation before persistence operations
- **Cache management** - Cache-aside pattern with tag-based invalidation
- **Repository orchestration** - Coordinate data access
- **Error handling** - Graceful degradation with null/empty returns

### Service pattern example

```csharp
public sealed class CustomerService(
    ICustomerRepository repo,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<CustomerApiModel> validator) : ICustomerService
{
    // Read with caching
    public async Task<CustomerApiModel?> GetCustomerByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose("administration", "customer", "v1", discriminator: $"by-id:{id}");
        return await cache.GetOrAddAsync<CustomerApiModel?>(key, async _ =>
        {
            return await repo.GetById(id);
        }, new CacheEntryOptions
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

        var created = await repo.Add(model.Convert());
        await cache.RemoveByTagAsync("administration:customer", ct);
        return created?.Convert();
    }
}
```

### Services by module

| Module         | Services                                                         |
|----------------|------------------------------------------------------------------|
| Administration | CustomerService, EmployeeService, GenreService, MediaTypeService |
| Music          | ArtistService, AlbumService, TrackService, PlaylistService       |
| Orders         | InvoiceService, InvoiceLineService                               |
| Identity       | TokenService, InMemoryUserStore, InMemoryRefreshTokenStore       |

## Validation with FluentValidation

All input validation uses FluentValidation with validators in
`SharedKernel.Persistence/Validation/`:

```csharp
public class CustomerValidator : AbstractValidator<CustomerApiModel>
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

Validators are auto-registered via assembly scanning:

```csharp
services.AddValidatorsFromAssemblyContaining<CustomerValidator>();
```

See [docs/validation-strategy.md](docs/validation-strategy.md) for complete
documentation.

## Endpoints

The host discovers all modules and composes their endpoints under conventional
groups:

- GET /api/music/health
- GET /api/music/data-health
- GET /api/orders/health
- GET /api/orders/data-health
- GET /api/administration/health
- GET /api/administration/data-health
- GET /api/reporting/health
- GET /api/reporting/data-health
- GET /api/identity/health
- GET /api/identity/data-health

Each returns HTTP 200 with JSON:

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

A root endpoint GET / returns similar metadata with module: "root".

## Build, run, and test

- Build: `dotnet build ModularMonolith.Api.sln`
- Run: `dotnet run --project src/ModularMonolith.Api`
- Swagger UI: http://localhost:5043/swagger (or the https port from launch
  settings)
- Tests: `dotnet test ModularMonolith.Api.sln`
    - Test coverage examples include root health, per-module health endpoints,
      and authenticated flows (e.g., obtaining a JWT and calling protected Music
      endpoints).
    - The host exposes a public partial Program class to support
      Microsoft.AspNetCore.Mvc.Testing’s WebApplicationFactory.

### Example curl commands

```
curl http://localhost:5043/api/music/health
curl http://localhost:5043/api/orders/health
curl http://localhost:5043/api/administration/health
curl http://localhost:5043/api/reporting/health
curl http://localhost:5043/api/identity/health
curl http://localhost:5043/
```

## Docker

Build and run the container:

```
docker build -t modular-monolith-api .
docker run -p 8080:8080 modular-monolith-api
```

- Dev ports vs Docker ports: When running locally via launchSettings.json the
  app listens on http://localhost:5043 and https://localhost:7043. In the
  container, ASPNETCORE_URLS is set to http://+:8080, so
  expose/browse http://localhost:8080.

Then browse http://localhost:8080/swagger

## Notes

### Rate limiting

- Minimal in-app rate limiting is enabled (Option A). The API host registers a
  named policy `global:public-anon` with a fixed window of 60 requests per 60
  seconds and applies it to the root endpoint (`GET /`).
- Central scaffolding still lives under `src/Shared/SharedKernel/TrafficControl`
  for future expansion, but wiring is now active via `AddRateLimiter(...)` and
  `UseRateLimiter()` in `Program.cs`.
- To protect additional endpoints, add
  `.RequireRateLimiting("global:public-anon")` (or other policies you add) to
  the desired endpoint mapping. To exempt an endpoint, use
  `.DisableRateLimiting()`.
- Example:

```
app.MapGet("/api/reporting/exports", Handler)
   .RequireRateLimiting("global:public-anon");
```

### Logging and observability

- Uses built-in ASP.NET Core logging by default. No Serilog or OpenTelemetry is
  wired out-of-the-box; you can add them later according to your needs.

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
- Swagger UI is enabled by default at /swagger when you run the API host.
- The OpenAPI document is generated with title "Modular Monolith API" (v1).
- JWT Bearer auth is integrated into Swagger:
    - Click the "Authorize" button in Swagger UI and paste the access token
      only (do NOT include the `Bearer ` prefix). Swagger will add it
      automatically.
    - In-memory login is only enabled in the `Development` or `Demo`
      environment when `Identity:InMemoryUsers` is configured.
- Once authorized, protected endpoints (e.g., Music Albums) can be executed
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
| `Permissions` | No | Each value becomes a `permissions` claim in the JWT. Authorization policies registered in `PolicyRegistry` use these values directly as policy names (for example `music.read`, `music.write`, `orders.read`, `orders.write`, `admin.users.manage`, `administration.read`, `administration.write`, `report.view`). |
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
  userId, roles, permissions, and tenant loaded from each valid in-memory user
  entry (never the password). If login succeeds but authorization is wrong,
  check that summary first.

##### Full user-secrets example

The following creates two development/demo accounts. The first can read Music
data in `tenant-1`; the second is an admin-oriented account in `tenant-admin`.

```bash
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Username" "demo"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Password" "<choose-a-strong-password>"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:UserId" "user-1"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:DisplayName" "Demo User"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Roles:0" "User"
dotnet user-secrets --project src/ModularMonolith.Api set "Identity:InMemoryUsers:0:Permissions:0" "music.read"
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

#### JWT for Music endpoints

- Where JWT is processed in code:
    - Global authentication/authorization is configured in
      `src/Modules/Identity/Identity.Module/Extensions/IdentityAuthExtensions.cs`.
    - Music endpoints opt-in to authorization using `.RequireAuthorization(...)`
      in each endpoint mapping.
    - Example: `GET /api/music/albums/{id}` is protected by `music.read` and
      `tenant.scoped` policies in
      `src/Modules/Music/Music.Module/Endpoints/AlbumEndpoints.cs`.
- How to use JWT in Swagger for Music endpoints:
    1) Call `POST /api/identity/login` to receive an `access_token`.
    2) In Swagger UI, click the Authorize button and enter: `<access_token>`.
    3) For tenant-scoped endpoints, set a tenant hint (current routes do not
       include a {tenant} segment by default):
        - Preferred: Header `X-Tenant-Id: <your-tenant>`.
        - Optional: A route value may be used if a module defines such a
          template in the future (e.g., `/api/music/{tenant}/albums/{id}` —
          hypothetical, not defined by default).
    4) Invoke Music endpoints. You will see:
        - 200 OK when the token includes `permissions: ["music.read"]` and
          tenant scope matches.
        - 403 Forbidden if missing permission or tenant mismatch.
        - 401 Unauthorized if no/invalid token is provided.

### New: Music Albums Endpoint (GET /api/music/albums/{id})

- Path: GET /api/music/albums/{id}
- Module: Music
- Authorization: Requires a valid JWT with the permission claim "music.read".
- Caching: Uses the central cache facade (ICacheFacade) with a namespaced key
  composed by CacheKeyComposer.
    - Key shape example: {env}:{app}:music:album:v1::::by-id:{id}
    - Default TTL: 20 minutes (with jitter to avoid stampede). Adjust via
      Caching:* configuration if needed.
- Data source: SQLite (chinook.db) via AppDbContext; includes Artist info.

How it works

- On request, the endpoint composes a cache key (module=music, entity=album,
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
curl -H "Authorization: Bearer $TOKEN" http://localhost:5043/api/music/albums/1
```

Notes

- To change caching behavior globally or per environment, use appsettings or
  environment variables under the Caching:* section. The cache is L1-only by
  default; you can enable L2 (e.g., Redis) later without code changes.
- If you later add write endpoints that mutate album data, evict the
  corresponding cache key(s) or bump the version prefix (v1→v2) to ensure
  readers don’t see stale data.

- Swagger is enabled with tags per module.
- CORS policy named "Default" allows common localhost dev origins: http(s):
  //localhost:3000, 4200, 5173.
- ProblemDetails middleware is enabled via UseExceptionHandler and
  AddProblemDetails.
- No cross-module references; only the host references the modules and
  SharedKernel.

## Data and persistence

- EF Core plan (single SQLite DbContext shared by all modules): see
  docs/EFCore-Plan.md
- Database file location: The app prefers
  src/ModularMonolith.Api/data/chinook.db (under the host content root) and
  falls back to repo-root /data/chinook.db if not found. At startup, Program.cs
  auto-detects the file and populates ConnectionStrings:AppDatabase when not
  provided.

### DbContext pooling and Repository pattern

#### Connection string configuration

- You can set the SQLite connection via appsettings (ConnectionStrings:
  AppDatabase), environment variables (ConnectionStrings__AppDatabase), or rely
  on auto-discovery in Program.cs which sets the key at runtime when not
  provided.

### Caching configuration

- Tier: Caching:Tier can be "L1" (default, in-memory only) or "L1L2" (adds an
  optional distributed cache if available/configured).
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
return await cache.GetOrAddAsync<CustomerApiModel?>(key, async _ => await repo.GetById(id),
    new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20), Tags = CustomerTags }, ct);

// Write with cache invalidation
await cache.RemoveByTagAsync(CustomerTags[0], ct);
```

See [docs/caching-strategy.md](docs/caching-strategy.md) for complete
documentation including service-level patterns.

This solution uses the Repository pattern with DbContext pooling for high
throughput:

- Repository interfaces: Defined in
  `src/Shared/SharedKernel.Persistence/Repositories/` (e.g., `IAlbumRepository`,
  `IArtistRepository`, etc.).
- Repository implementations: Defined in
  `src/Shared/SharedKernel.DataSQLite/Repositories/` with a `BaseRepository<T>`
  providing common CRUD operations.
- How the DbContext is registered:
  `AddDbContextPool<AppDbContext>(..., poolSize: 128)` in
  `src/Shared/SharedKernel.Persistence/PersistenceRegistration.cs`.
- Why pooling matters: The 128 AppDbContext instances resolved from DI are
  reused, reducing allocations and connection overhead.
- How modules consume data: Inject repository interfaces (e.g.,
  `IAlbumRepository`) or `IAppDbContext`/`AppDbContext` directly in endpoint
  handlers.

### How modules access data

Modules access data via repository interfaces or the DbContext directly:

**Option 1: Repository pattern (preferred)**

- Inject repository interfaces in endpoint handlers:
  ```csharp
  group.MapGet("/albums/{id}", async (int id, IAlbumRepository repo, CancellationToken ct) =>
  {
      var album = await repo.GetByIdAsync(id, ct);
      return album is null ? Results.NotFound() : Results.Ok(album);
  });
  ```

**Option 2: Direct DbContext access**

- For health checks or custom queries, inject `AppDbContext` directly:
  ```csharp
  group.MapGet("/data-health", async (AppDbContext db, CancellationToken ct) =>
  {
      var ok = await db.Database.CanConnectAsync(ct);
      // ...
  });
  ```

**Option 3: IAppDbContext abstraction**

- For services that need EF Core but want to avoid concrete DbContext
  dependency:
  ```csharp
  public sealed class MyService(IAppDbContext db)
  {
      public async Task<List<Album>> GetAlbumsAsync(CancellationToken ct)
          => await db.Set<Album>().ToListAsync(ct);
  }
  ```

Notes:

- Repository interfaces are in `SharedKernel.Persistence/Repositories/`
- Repository implementations are in `SharedKernel.DataSQLite/Repositories/`
- The host registers all repositories in Program.cs
- The host computes an absolute SQLite Data Source to data/chinook.db at startup

## Documentation

Detailed documentation is available in the `/docs` folder:

### Architecture & Implementation

- [Services Architecture](docs/services-architecture.md) - Service layer
  patterns, caching integration, and validation
- [Validation Strategy](docs/validation-strategy.md) - FluentValidation
  implementation and patterns
- [Caching Strategy](docs/caching-strategy.md) - Multi-tier caching with
  tag-based invalidation
- [EF Core Plan](docs/EFCore-Plan.md) - Database architecture and repository
  pattern
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

The solution includes comprehensive integration tests in
`tests/ModularMonolith.Api.Tests/`:

| Test Category      | Description                                       |
|--------------------|---------------------------------------------------|
| Health endpoints   | Module health and data-health endpoint tests      |
| Identity endpoints | Login, refresh, logout, userinfo, JWKS tests      |
| Write operations   | POST/PUT/DELETE endpoint tests with authorization |
| Rate limiting      | 429 response behavior tests                       |
| Caching behavior   | Cache consistency and stampede prevention tests   |
| Error scenarios    | Invalid JSON, validation errors, edge cases       |

Run tests with coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Current coverage: **73% line coverage** across all modules
