# ModularMonolith.Api (ASP.NET Core 9 Minimal APIs)

Production-ready modular monolith starter using ASP.NET Core 9 (net9.0) and Minimal APIs. It demonstrates module composition via a simple IModule contract, clear boundaries, and integration tests.

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


## Construction plan: creating the solution and all projects (step-by-step)

This section is a detailed, prescriptive plan for creating this modular monolith from scratch, including the order of work, exact commands, and verification steps. It assumes you are using the .NET SDK and the dotnet CLI. Adjust names/paths as needed if you diverge.

High-level order of work
- 0. Prerequisites and repo bootstrap
- 1. Create the solution scaffolding and shared build settings
- 2. Create the SharedKernel project (cross-cutting primitives and module contract)
- 3. Create module projects (Music, Orders, Administration, Reporting, Identity)
- 4. Create the host web project (ModularMonolith.Api) and wire module discovery
- 5. Add integration tests (ModularMonolith.Api.Tests)
- 6. Add EF Core infrastructure with a single SQLite DbContext shared by all modules
- 7. Add Docker packaging
- 8. Verify (build, run, test)

0) Prerequisites and repo bootstrap
- Install .NET SDK 9 or newer (this solution targets net10.0 in project files; SDK 9 image supports it with preview LangVersion where used).
- Create and initialize the repository directory structure:
  - Folders: src, src/Modules/{Music,Orders,Administration,Reporting,Identity}, src/Shared/SharedKernel, tests.
  - Optional: add a Git repo and .gitignore (use dotnet new gitignore).

1) Create the solution scaffolding and shared build settings
- Create the solution file:
  - dotnet new sln -n ModularMonolith.Api
- Add a Directory.Build.props with common settings (nullable, implicit usings, etc.). Example:
  - <Project>
      <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
        <LangVersion>preview</LangVersion>
      </PropertyGroup>
    </Project>
- Commit after each stable step.

2) Create the SharedKernel project
- Purpose: contains cross-cutting primitives, the IModule contract, and common attributes/utilities. No infrastructure/persistence here.
- Commands:
  - cd src/Shared
  - dotnet new classlib -n SharedKernel
  - dotnet add SharedKernel/SharedKernel.csproj framework-reference Microsoft.AspNetCore.App
  - dotnet sln ../../ModularMonolith.Api.sln add SharedKernel/SharedKernel.csproj
- Add IModule contract (SharedKernel/IModule.cs):
  - public interface IModule { string Name { get; } void RegisterServices(IServiceCollection services, IConfiguration config); void MapEndpoints(IEndpointRouteBuilder endpoints); }

3) Create module projects
- Purpose: encapsulate feature areas; each module implements IModule and exposes endpoints under /api/{module}.
- Commands (repeat for each module):
  - cd src/Modules/Music && dotnet new classlib -n Music.Module
  - cd ../Orders && dotnet new classlib -n Orders.Module
  - cd ../Administration && dotnet new classlib -n Admin.Module
  - cd ../Reporting && dotnet new classlib -n Reporting.Module
  - cd ../Identity && dotnet new classlib -n Identity.Module
- Reference SharedKernel from every module project:
  - dotnet add src/Modules/Music/Music.Module/Music.Module.csproj reference src/Shared/SharedKernel/SharedKernel.csproj
  - Repeat for the other modules.
- Add a public module type that implements IModule in each project and maps a GET /api/{module}/health endpoint returning basic metadata.
- Add all module projects to the solution:
  - dotnet sln ModularMonolith.Api.sln add src/Modules/*/*/*.csproj

4) Create the host web project (ModularMonolith.Api)
- Purpose: ASP.NET Core Minimal API host that composes all modules.
- Commands:
  - dotnet new web -n ModularMonolith.Api -o src/ModularMonolith.Api
  - dotnet add src/ModularMonolith.Api/ModularMonolith.Api.csproj package Swashbuckle.AspNetCore
  - Add project references to SharedKernel and all module projects:
    - dotnet add src/ModularMonolith.Api/ModularMonolith.Api.csproj reference src/Shared/SharedKernel/SharedKernel.csproj src/Modules/Music/Music.Module/Music.Module.csproj src/Modules/Orders/Orders.Module/Orders.Module.csproj src/Modules/Administration/Admin.Module/Admin.Module.csproj src/Modules/Reporting/Reporting.Module/Reporting.Module.csproj src/Modules/Identity/Identity.Module/Identity.Module.csproj
  - dotnet sln ModularMonolith.Api.sln add src/ModularMonolith.Api/ModularMonolith.Api.csproj
- Implement Program.cs:
  - Add Swagger, CORS, ProblemDetails.
  - Implement reflection-based module discovery (scan for IModule types) and deterministic ordering.
  - Map a root GET / endpoint with service metadata.
  - Map module endpoints by calling module.MapEndpoints.

5) Add integration tests (ModularMonolith.Api.Tests)
- Purpose: verify host composes modules and endpoints respond.
- Commands:
  - dotnet new xunit -n ModularMonolith.Api.Tests -o tests/ModularMonolith.Api.Tests
  - dotnet add tests/ModularMonolith.Api.Tests/ModularMonolith.Api.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
  - dotnet add tests/ModularMonolith.Api.Tests/ModularMonolith.Api.Tests.csproj reference src/ModularMonolith.Api/ModularMonolith.Api.csproj
  - dotnet sln ModularMonolith.Api.sln add tests/ModularMonolith.Api.Tests/ModularMonolith.Api.Tests.csproj
- Implement tests using WebApplicationFactory<Program> to call module health endpoints and the root endpoint.

6) Add EF Core infrastructure with a single SQLite DbContext
- Goals: one DbContext for the entire app, SQLite provider, DB file at /data/chinook.db, shared across modules.
- Follow the dedicated plan here: docs/EFCore-Plan.md
- Summary of creation steps:
  - Create SharedKernel.Persistence class library under src/Shared/SharedKernel.Persistence.
  - Add packages: Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.EntityFrameworkCore.Design.
  - Implement AppDbContext and IAppDbContext, DI registration extension (AddKernelPersistence), and a design-time factory.
  - In Program.cs (host): compute absolute Data Source to data/chinook.db under ContentRoot; set ConnectionStrings:AppDatabase and call AddKernelPersistence; ensure DB is created in Development (EnsureCreated or Migrate once migrations exist).
  - Keep migrations in SharedKernel.Persistence. Use dotnet ef migrations add <Name> from that project.

7) Add Docker packaging
- Create a multi-stage Dockerfile that restores, builds, and publishes the host app, then runs on aspnet runtime image.
- Expose port (e.g., 8080), set ASPNETCORE_URLS, and copy publish output.
- If you need the SQLite file inside the image, create /app/data and copy data/chinook.db or mount a volume at runtime (see docs/EFCore-Plan.md for details).

8) Verify (build, run, test)
- Build the entire solution:
  - dotnet build ModularMonolith.Api.sln
- Run the API locally:
  - dotnet run --project src/ModularMonolith.Api
- Browse Swagger at http://localhost:<port>/swagger and call the health endpoints.
- Run tests:
  - dotnet test ModularMonolith.Api.sln

Detailed checklist with commands
1. Create solution and shared props
   - dotnet new sln -n ModularMonolith.Api
   - Create Directory.Build.props (see snippet above)
2. Create SharedKernel
   - dotnet new classlib -n SharedKernel -o src/Shared/SharedKernel
   - dotnet add src/Shared/SharedKernel/SharedKernel.csproj framework-reference Microsoft.AspNetCore.App
   - Add IModule interface
   - dotnet sln ModularMonolith.Api.sln add src/Shared/SharedKernel/SharedKernel.csproj
3. Create modules and wire to SharedKernel
   - dotnet new classlib -n Music.Module -o src/Modules/Music/Music.Module
   - dotnet new classlib -n Orders.Module -o src/Modules/Orders/Orders.Module
   - dotnet new classlib -n Admin.Module -o src/Modules/Administration/Admin.Module
   - dotnet new classlib -n Reporting.Module -o src/Modules/Reporting/Reporting.Module
   - dotnet new classlib -n Identity.Module -o src/Modules/Identity/Identity.Module
   - dotnet add src/Modules/*/*/*.csproj reference src/Shared/SharedKernel/SharedKernel.csproj (repeat per project)
   - Implement a Module : IModule with GET /api/{module}/health in each module
   - dotnet sln ModularMonolith.Api.sln add src/Modules/*/*/*.csproj
4. Create host and compose modules
   - dotnet new web -n ModularMonolith.Api -o src/ModularMonolith.Api
   - dotnet add src/ModularMonolith.Api/ModularMonolith.Api.csproj package Swashbuckle.AspNetCore
   - dotnet add src/ModularMonolith.Api/ModularMonolith.Api.csproj reference src/Shared/SharedKernel/SharedKernel.csproj src/Modules/Music/Music.Module/Music.Module.csproj src/Modules/Orders/Orders.Module/Orders.Module.csproj src/Modules/Administration/Admin.Module/Admin.Module.csproj src/Modules/Reporting/Reporting.Module/Reporting.Module.csproj src/Modules/Identity/Identity.Module/Identity.Module.csproj
   - Implement Program.cs with discovery and mapping
5. Tests
   - dotnet new xunit -n ModularMonolith.Api.Tests -o tests/ModularMonolith.Api.Tests
   - dotnet add tests/ModularMonolith.Api.Tests/ModularMonolith.Api.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
   - dotnet add tests/ModularMonolith.Api.Tests/ModularMonolith.Api.Tests.csproj reference src/ModularMonolith.Api/ModularMonolith.Api.csproj
   - Implement integration tests for health endpoints and root
6. EF Core (single DbContext)
   - Create src/Shared/SharedKernel.Persistence and implement per docs/EFCore-Plan.md
   - Reference SharedKernel.Persistence from the host and any modules that need IAppDbContext
   - Ensure data/chinook.db path resolution and EnsureCreated/Migrate in Development
   - Create initial migrations when entities are added
7. Docker
   - Create Dockerfile with multi-stage build and runtime
   - Optionally copy/mount data/chinook.db; expose port and set ASPNETCORE_URLS
8. Verify and iterate
   - dotnet build; dotnet run; dotnet test
   - Add CI/CD later (GitHub Actions) to run build and tests on push

Notes and conventions
- Target frameworks: projects currently use net10.0 in csproj files. Keep consistent across projects.
- Modules should not reference each other; only the host references modules and SharedKernel.
- Persistence is centralized in SharedKernel.Persistence; modules depend on its abstractions (IAppDbContext) but not on EF Core specifics where possible.
- For EF Core details and migration commands, see docs/EFCore-Plan.md.
