### Step-by-step walkthrough: Recreate this Modular Monolith solution from scratch

This guide shows a developer how to build this solution from nothing using the .NET SDK and the same architectural choices found in this repo.

It is divided into small, verifiable steps. Commands assume macOS/Linux bash; on Windows, use PowerShell equivalents.

Prerequisites
- .NET SDK 10.0 or later (individual csproj files target `net10.0`, overriding `Directory.Build.props`)
- An editor or IDE (JetBrains Rider recommended)
- Optional: Docker (to run in a container)

1. Create the solution scaffold
- Create the repo folder and solution file:
  - mkdir aspnetcore-min-api-modular-monolithic && cd $_
  - dotnet new sln -n ModularMonolith.Api
- Create the API host project (Minimal API):
  - dotnet new web -n ModularMonolith.Api -o src/ModularMonolith.Api
- Create shared and module projects (class libraries):
  - dotnet new classlib -n SharedKernel -o src/Shared/SharedKernel
  - dotnet new classlib -n SharedKernel.Persistence -o src/Shared/SharedKernel.Persistence
  - dotnet new classlib -n SharedKernel.DataSQLite -o src/Shared/SharedKernel.DataSQLite
  - dotnet new classlib -n Music.Module -o src/Modules/Music/Music.Module
  - dotnet new classlib -n Orders.Module -o src/Modules/Orders/Orders.Module
  - dotnet new classlib -n Admin.Module -o src/Modules/Administration/Admin.Module
  - dotnet new classlib -n Reporting.Module -o src/Modules/Reporting/Reporting.Module
  - dotnet new classlib -n Identity.Module -o src/Modules/Identity/Identity.Module
- Create tests project:
  - dotnet new xunit -n ModularMonolith.Api.Tests -o tests/ModularMonolith.Api.Tests
- Add all projects to the solution:
  - dotnet sln ModularMonolith.Api.sln add src/**/**/*.csproj src/**/**/**/*.csproj src/ModularMonolith.Api/ModularMonolith.Api.csproj tests/ModularMonolith.Api.Tests/ModularMonolith.Api.Tests.csproj

2. Centralize build settings with Directory.Build.props
- At the repo root, create Directory.Build.props with:
  - TargetFramework net9.0 (note: individual csproj files may override this to net10.0)
  - Nullable enable, ImplicitUsings enable
  - TreatWarningsAsErrors true (optional)
  - LangVersion preview (optional)

3. Define the module contract in SharedKernel
- Create IModule:
  - public interface IModule { string Name { get; } void RegisterServices(IServiceCollection, IConfiguration); void MapEndpoints(IEndpointRouteBuilder); }
- SharedKernel may also include small cross-cutting helpers and abstractions (but avoid shared business logic).

4. Persistence layer (SharedKernel.Persistence)
- Add EF Core packages:
  - Microsoft.EntityFrameworkCore
  - Microsoft.EntityFrameworkCore.Sqlite
  - Microsoft.EntityFrameworkCore.Design (PrivateAssets=All)
- Create an AppDbContext (read-only for Chinook demo data)
- Provide registration extension AddKernelPersistence(IConfiguration) that:
  - Reads ConnectionStrings:AppDatabase (SQLite path)
  - Registers DbContextFactory<AppDbContext> (or DbContext) with Sqlite
- Add simple API models, converters, and validation helpers as needed (used by modules).
- Seed database file: place data/chinook.db under src/ModularMonolith.Api/data and optionally at repo root /data to simplify dev paths.

5. Identity module (authentication and authorization)
- In Identity.Module add extensions:
  - AddIdentityAuth(IConfiguration) to register auth services:
    - Authentication: JWT Bearer (use a static signing key for dev or configuration-driven key)
    - Authorization: register policies and handlers (e.g., tenant authorization)
    - Add minimal endpoints for login issuing demo JWTs
  - UseIdentityAuth() to add the middleware (UseAuthentication + UseAuthorization)
- Add an Authorization folder with:
  - PolicyRegistry defining names (e.g., Policies.MusicRead)
  - Custom handlers (e.g., TenantAuthorizationHandler) and services (e.g., HttpContextTenantResolutionService)
- Add Endpoints:
  - HealthEndpoints & DataHealthEndpoints (GET /api/identity/health, /api/identity/data/health)
  - Login endpoint (POST /api/identity/login) that validates demo users and returns a JWT

6. Feature modules following the module pattern
- Each module exposes a static <Feature>Module with nested public sealed class Modules : IModule
  - Example Music.Modules.MusicModule.Modules implements Name => "Music"
  - RegisterServices: register internal services if any (can be empty initially)
  - MapEndpoints: map health endpoints and the feature endpoints under /api/<feature>
- Music module sample endpoints:
  - group.MapGet("/health", ...) returning module health JSON
  - group.MapGet("/data/health", ...) checking AppDbContext and returning DB status
  - Album endpoints (GET /albums, GET /albums/{id}) reading from AppDbContext with minimal DTOs
- Mirror pattern for Orders, Administration, Reporting with their own Health endpoints.

7. API host composition (src/ModularMonolith.Api/Program.cs)
- Minimal API setup:
  - Configure JsonOptions (e.g., keep exact property naming if desired)
  - AddEndpointsApiExplorer and SwaggerGen
  - AddProblemDetails and CORS with a "Default" policy
  - Register Identity auth services via AddIdentityAuth(builder.Configuration)
  - Register central caching via SharedKernel.Caching (IMemoryCache by default)
- Connection string resolution for SQLite:
  - If ConnectionStrings:AppDatabase not specified, locate data/chinook.db either under host content root /data or repo /data, then set builder.Configuration["ConnectionStrings:AppDatabase"] = $"Data Source={path}"
  - Call services.AddKernelPersistence(builder.Configuration)
- Build the app, add middleware:
  - UseExceptionHandler, UseStatusCodePages, UseCors("Default")
  - UseIdentityAuth() to wire authentication/authorization
  - UseSwagger & UseSwaggerUI
- Root endpoint GET / returning service metadata (module: "root")
- Compose modules:
  - Instantiate all Modules : IModule (Administration, Identity, Music, Orders, Reporting)
  - For each module: module.RegisterServices(services, config)
  - After building: module.MapEndpoints(app)

8. Central caching helpers (SharedKernel.Caching)
- Add AddCentralCaching(IConfiguration) extension to register IMemoryCache and optionally a level-2 cache if configured.

9. Testing with WebApplicationFactory (tests/ModularMonolith.Api.Tests)
- Reference Microsoft.AspNetCore.Mvc.Testing and xUnit packages
- Expose Program partial class in the host (public partial class Program) to support WebApplicationFactory
- Write integration tests that spin up the API and assert responses for:
  - GET / (root health)
  - GET /api/<module>/health for each module
  - Authenticated flows (optional): obtain token via POST /api/identity/login and call protected endpoints

10. Developer experience and Swagger
- Configure Swagger security scheme (bearer) and security requirements so you can authorize once in Swagger UI
- Tip for Swagger: instruct users to paste the token without the "Bearer " prefix; Swagger adds it automatically
- Provide example demo users in the README to try protected endpoints

11. Running and verifying
- Build: dotnet build ModularMonolith.Api.sln
- Run: dotnet run --project src/ModularMonolith.Api
- Browse Swagger UI: http://localhost:5043/swagger (or the port printed by the app)
- Example curl calls:
  - curl http://localhost:5043/
  - curl http://localhost:5043/api/music/health
  - curl http://localhost:5043/api/identity/health
- Obtain a JWT (example):
  - curl -X POST http://localhost:5043/api/identity/login -H 'Content-Type: application/json' -d '{"username":"demo","password":"demo123!"}'
  - Use the access_token in Swagger UI Authorize dialog

12. Optional: Dockerize
- Add a Dockerfile to build and run the host app
- docker build -t modular-monolith-api .
- docker run -p 8080:8080 modular-monolith-api

13. Conventions and tips used in this repo
- Keep modules independent; only IModule is the contract seen by the host
- Default types inside modules should be internal; only the module entry is public
- Group all module endpoints under /api/<feature>
- Prefer small endpoint classes per resource (e.g., AlbumEndpoints) and have the module entry compose them
- Keep shared kernel minimal and business-agnostic
- Favor integration tests at the host boundary

Where to look in this repository for concrete references
- Module contract: src/Shared/SharedKernel/IModule.cs
- Host composition and middleware: src/ModularMonolith.Api/Program.cs
- Music module example: src/Modules/Music/Music.Module/Module.cs and Endpoints
- Identity auth setup: src/Modules/Identity/Identity.Module/Extensions/IdentityAuthExtensions.cs
- Authorization examples: src/Modules/Identity/Identity.Module/Authorization/*
- Health endpoints patterns: all modules' Endpoints folders
- Persistence bootstrap: src/Shared/SharedKernel.Persistence/* and Program.cs connection string resolution

FAQ
- Why Minimal APIs? Small surface, quick composition across modules, and simple testing.
- Why SQLite? For demo and local dev. Replace with SQL Server or PostgreSQL in production; the registration extension abstracts that.
- How do modules communicate? Via shared kernel abstractions or direct calls within the same process; keep boundaries clean even in-process.
