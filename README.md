# ModularMonolith.Api (ASP.NET Core 9 Minimal APIs)

Production-ready modular monolith starter using ASP.NET Core 9 (net9.0) and Minimal APIs. It demonstrates module composition via a simple IModule contract, clear boundaries, and integration tests.

Looking to recreate this solution from scratch? See the step-by-step guide in docs/Walkthrough.md.

## Solution layout

```
/src
  /ModularMonolith.Api                (ASP.NET Core 9 Web API host, Minimal APIs)
  /Modules
    /Music/Music.Module               (Class Library)
    /Orders/Orders.Module             (Class Library)
    /Administration/Admin.Module      (Class Library)
    /Reporting/Reporting.Module       (Class Library)
    /Identity/Identity.Module         (Class Library)
  /Shared/SharedKernel                (Class Library for cross-cutting primitives only)
/tests
  /ModularMonolith.Api.Tests          (xUnit integration tests using WebApplicationFactory)
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

Each module exposes a public static <ModuleName>Module with a nested public sealed class Module : IModule that registers services and maps endpoints. Only the IModule is public outside the module; all other types should remain internal by default.

## Endpoints

The host discovers all modules and composes their endpoints under conventional groups:

- GET /api/music/health
- GET /api/orders/health
- GET /api/administration/health
- GET /api/reporting/health
- GET /api/identity/health

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
- Swagger UI: http://localhost:5043/swagger (or the https port from launch settings)
- Tests: `dotnet test ModularMonolith.Api.sln`

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

Then browse http://localhost:8080/swagger

## Notes

### Using Swagger & OpenAPI
- Swagger UI is enabled by default at /swagger when you run the API host.
- The OpenAPI document is generated with title "Modular Monolith API" (v1).
- JWT Bearer auth is integrated into Swagger:
  - Click the "Authorize" button in Swagger UI and paste the access token only (do NOT include the `Bearer ` prefix). Swagger will add it automatically.
  - Obtain a token via `POST /api/identity/login` with one of the demo users below.
- Once authorized, protected endpoints (e.g., Music Albums) can be executed directly from Swagger UI.

#### Demo users and module access
Use these credentials with `POST /api/identity/login` to receive an access_token:

- demo / demo123! — Music module only
  - permissions: [music.read]
  - roles: [User]
  - tenant: tenant-1
- usermo / usermo123! — Music and Orders modules
  - permissions: [music.read, orders.read]
  - roles: [User]
  - tenant: tenant-1
- report / report123! — Reporting module only
  - permissions: [report.view]
  - roles: [User]
  - tenant: tenant-1
- admin / admin123! — All modules (administrator)
  - permissions: [music.read, music.write, orders.read, orders.write, admin.users.manage, report.view]
  - roles: [Admin]
  - tenant: tenant-1

#### JWT for Music endpoints
- Where JWT is processed in code:
  - Global authentication/authorization is configured in `src/Modules/Identity/Identity.Module/Extensions/IdentityAuthExtensions.cs`.
  - Music endpoints opt-in to authorization using `.RequireAuthorization(...)` in each endpoint mapping.
  - Example: `GET /api/music/albums/{id}` is protected by `music.read` and `tenant.scoped` policies in `src/Modules/Music/Music.Module/Endpoints/AlbumEndpoints.cs`.
- How to use JWT in Swagger for Music endpoints:
  1) Call `POST /api/identity/login` to receive an `access_token`.
  2) In Swagger UI, click the Authorize button and enter: `<access_token>`.
  3) For tenant-scoped endpoints, optionally set a tenant hint:
     - Route value: e.g., `/api/music/{tenant}/albums/{id}` if such route is defined, or
     - Header: `X-Tenant-Id: <your-tenant>`.
  4) Invoke Music endpoints. You will see:
     - 200 OK when the token includes `permissions: ["music.read"]` and tenant scope matches.
     - 403 Forbidden if missing permission or tenant mismatch.
     - 401 Unauthorized if no/invalid token is provided.

### New: Music Albums Endpoint (GET /api/music/albums/{id})
- Path: GET /api/music/albums/{id}
- Module: Music
- Authorization: Requires a valid JWT with the permission claim "music.read".
- Caching: Uses the central cache facade (ICacheFacade) with a namespaced key composed by CacheKeyComposer.
  - Key shape example: {env}:{app}:music:album:v1::::by-id:{id}
  - Default TTL: 20 minutes (with jitter to avoid stampede). Adjust via Caching:* configuration if needed.
- Data source: SQLite (chinook.db) via AppDbContext; includes Artist info.

How it works
- On request, the endpoint composes a cache key (module=music, entity=album, version=v1, discriminator=by-id:{id}).
- It calls cache.GetOrAddAsync(key, factory) where factory queries the database if the cache is missed.
- Non-existent IDs return 404 (not cached). Existing albums are cached for faster subsequent reads.
- The endpoint is protected; include Authorization: Bearer <token> header. Obtain a token via POST /api/identity/login with demo credentials: {"username":"demo","password":"demo123!"}.

Example
1) Get token
```
curl -s -X POST http://localhost:5043/api/identity/login \
  -H "Content-Type: application/json" \
  -d '{"username":"demo","password":"demo123!"}'
```
Response contains access_token.

2) Fetch album 1 using token
```
TOKEN="<paste-access-token>"
curl -H "Authorization: Bearer $TOKEN" http://localhost:5043/api/music/albums/1
```

Notes
- To change caching behavior globally or per environment, use appsettings or environment variables under the Caching:* section. The cache is L1-only by default; you can enable L2 (e.g., Redis) later without code changes.
- If you later add write endpoints that mutate album data, evict the corresponding cache key(s) or bump the version prefix (v1→v2) to ensure readers don’t see stale data.

- Swagger is enabled with tags per module.
- CORS policy named "Default" allows common localhost dev origins.
- ProblemDetails middleware is enabled via UseExceptionHandler and AddProblemDetails.
- No cross-module references; only the host references the modules and SharedKernel.

## Data and persistence

- EF Core plan (single SQLite DbContext shared by all modules): see docs/EFCore-Plan.md
- The database file is expected at /data/chinook.db (solution root). The plan explains registration and connection string handling.

### How modules access the DbContext

All modules share a single DbContext registered by the host via AddKernelPersistence. Modules can access it through ASP.NET Core DI:

- For endpoint handlers (Minimal APIs), inject AppDbContext directly in the delegate parameters (scope is per-request):

  Example (from Administration module):
  - group.MapGet("/data-health", async (AppDbContext db, IHostEnvironment env, IConfiguration cfg, CancellationToken ct) => { var ok = await db.Database.CanConnectAsync(ct); /* ... */ });

- For application services/handlers inside a module, depend on the abstraction IAppDbContext (preferred to keep EF types out of most code):

  public sealed class GetSomethingHandler
  {
      private readonly IAppDbContext _db;
      public GetSomethingHandler(IAppDbContext db) => _db = db;
      public async Task<IReadOnlyList<Thing>> Handle(CancellationToken ct) => await _db.Set<Thing>().ToListAsync(ct);
  }

Notes:
- Modules should reference SharedKernel.Persistence if they need the IAppDbContext or AppDbContext types.
- The host computes an absolute SQLite Data Source to data/chinook.db at startup and sets ConnectionStrings:AppDatabase accordingly (see Program.cs).
