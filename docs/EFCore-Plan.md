### EF Core plan for the modular monolith (SQLite, single DbContext)

**Status: Implemented**

This document outlines how Entity Framework Core is integrated into this modular monolith:
- A single DbContext (`AppDbContext`) serves the entire app.
- All modules access the same SQLite database (Chinook) via `IAppDbContext` or `AppDbContext`.
- The database file is `data/chinook.db` located under the host content root or at the solution root.
- Repository pattern implemented via `SharedKernel.Persistence` (interfaces) and `SharedKernel.DataSQLite` (implementations).


Goals
- One database, one DbContext, many modules.
- Keep module boundaries: modules depend only on SharedKernel abstractions, not on the host or on each other.
- Centralized migrations and schema ownership to avoid conflicts while allowing per‑module schema evolution.
- Simple local dev story with a stable SQLite file path that works for CLI, Rider/VS, Docker.


High‑level architecture
- SharedKernel.Persistence (new class library)
  - Contains the single EF Core DbContext (AppDbContext) and related configuration extensions.
  - Exposes a minimal IAppDbContext abstraction for modules that prefer not to depend on EF Core types directly.
  - Hosts EF Core migrations.
- Host (ModularMonolith.Api)
  - Registers the DbContext and provides the connection string (pointing to /data/chinook.db).
  - Manages database lifecycle at startup (EnsureCreated/ApplyMigrations in Development/Test as needed).
- Modules (e.g., Music.Module, Orders.Module, etc.)
  - Reference SharedKernel and, optionally, SharedKernel.Persistence only through abstractions.
  - Prefer depending on IAppDbContext or repositories defined in each module (but implemented using AppDbContext).


Project layout changes
1) Create project: src/Shared/SharedKernel.Persistence/SharedKernel.Persistence.csproj
   - References: Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.EntityFrameworkCore.Design
   - TargetFramework: align with the solution TFMs (currently net10.0 in project files).

2) Add AppDbContext in the new project
   - public sealed class AppDbContext : DbContext
   - Initially empty DbSets (you can add per‑module entities later via partials or in this project with a per‑module folder).
   - Conventions configured in OnModelCreating (UTC DateTime, string lengths, etc.).

3) Add IAppDbContext abstraction (optional but recommended)
   - public interface IAppDbContext
     - Expose minimal members used broadly: DbSet<T> Set<T>(), Task<int> SaveChangesAsync(...)
     - Avoid exposing EF‑specific APIs to modules if you want to keep strict boundaries.
   - AppDbContext implements IAppDbContext.

4) Registration extensions
   - public static class PersistenceRegistration
     - AddKernelPersistence(this IServiceCollection, IConfiguration):
       - Adds AppDbContext with AddDbContextPool<AppDbContext> using SQLite connection string.
       - Registers IAppDbContext to resolve as AppDbContext.
     - Connection string name: AppDatabase (see below).

5) Design‑time factory for migrations
   - class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
     - Builds DbContextOptions using the same connection string rules as runtime (see Connection string and path).
   - Enables running `dotnet ef migrations add` from the SharedKernel.Persistence project folder.


Connection string and path
- Connection string name: AppDatabase
- Value (in host appsettings.json): Data Source=<absolute path to solution>/data/chinook.db
- Build absolute path at runtime from the host content root:
  - var dataPath = Path.Combine(app.Environment.ContentRootPath, "data", "chinook.db");
  - var connectionString = $"Data Source={dataPath}";
- Alternative: keep a literal in appsettings.Development.json but normalize to absolute at startup so EF gets a full path.
- Ensure the /data directory exists in local dev (repo already contains data/chinook.db).


Registration in the host (ModularMonolith.Api)
- During Program.cs service configuration:
  - builder.Services.AddKernelPersistence(builder.Configuration);
- Optionally, at startup:
  - If (Development or Test): apply migrations automatically or EnsureCreated for an initial bring‑up.
- Example (pseudo‑code):

  builder.Services.AddKernelPersistence(builder.Configuration);

  var app = builder.Build();

  if (app.Environment.IsDevelopment())
  {
      using var scope = app.Services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.Database.EnsureCreated(); // or db.Database.Migrate(); if using migrations
  }


How modules access the DbContext
- Option A (recommended): depend on IAppDbContext (from SharedKernel.Persistence) in module services/handlers, and use Set<T>() to query/update entities.
- Option B: define per‑module repositories (interfaces in the module, implementations in the module) that internally depend on IAppDbContext.
- Avoid making modules depend on the host project. They should only reference SharedKernel (+ SharedKernel.Persistence for EF abstractions).
- You can keep entities outside modules (SharedKernel.Persistence/Entities/<ModuleName>) if you want a single schema project, or keep aggregates closer to modules and map them via partial OnModelCreating contributions (see next section).


Entity type configuration organization (mapping)
- Prefer per‑module folders under SharedKernel.Persistence:
  - Entities/Music
  - Entities/Orders
  - Entities/Administration
  - Entities/Reporting
  - Entities/Identity
- Use IEntityTypeConfiguration<T> classes per entity and apply configurations in AppDbContext.OnModelCreating via ModelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly).
- If you want modules to contribute their mappings without depending on EF:
  - Define a small contract IModelBuilderContributor in SharedKernel.Persistence with a method: void Contribute(ModelBuilder modelBuilder)
  - Modules can register IModelBuilderContributor implementations via DI, and AppDbContext can discover and apply them. This is optional and more advanced.


Migrations strategy
- Keep all migrations in SharedKernel.Persistence.
- Workflow:
  1. Modify or add entity configurations.
  2. From src/Shared/SharedKernel.Persistence: `dotnet ef migrations add <Name>`
  3. Commit the generated migration files.
  4. At runtime in Dev/Test: call Database.Migrate() or EnsureCreated(). In Production: prefer migrations run in CI/CD before app starts.
- Versioning and ownership:
  - Prefix migration names with the module (e.g., Music_Initial, Orders_AddOrderItem) to signal ownership.


Transactions and cross‑module operations
- The single DbContext enables atomic multi‑aggregate operations across modules when required. Keep cross‑module transactions rare.
- Use ambient transactions scoped to a single request. The default per‑request DbContext lifetime is Scoped.
- If you need an explicit unit of work, expose it via SharedKernel.Persistence (e.g., IUnitOfWork implemented by AppDbContext).


Performance and resilience
- Use AddDbContextPool for connection pooling.
- With SQLite, concurrency is limited; prefer short transactions and avoid long‑running read locks.
- Consider `PRAGMA journal_mode = WAL` for better concurrency if needed (can be set via connection string or OnConfiguring).


Testing
- Integration tests can either:
  - Use the same SQLite file but redirect to a temporary path under the test’s working folder; or
  - Use SQLite in‑memory mode with connection string "DataSource=:memory:" and keep the connection open for test lifetime.
- Provide a test helper to override the AppDatabase connection string in WebApplicationFactory.


Docker and deployment
- Ensure the /data folder is created in the container image and the file is copied/mounted:
  - Dockerfile: `RUN mkdir -p /app/data` and `COPY data/chinook.db /app/data/`
  - Use ContentRoot /app to resolve absolute path for the connection string at runtime.
- For production, consider migrating away from SQLite to a server RDBMS; the AppDbContext and repository patterns remain the same with a provider change.


Security and access control
- Enforce data access rules in repositories or domain services within modules.
- Avoid exposing DbContext directly to controllers/endpoints; use services that encapsulate queries and commands.


Current implementation status (updated 2026-01)
- SharedKernel.Persistence project:
  - `AppDbContext` with full Chinook schema: Albums, Artists, Customers, Employees, Genres, Invoices, InvoiceLines, MediaTypes, Playlists, PlaylistTracks, Tracks.
  - `IAppDbContext` abstraction for modules preferring to avoid direct EF Core dependency.
  - `PersistenceRegistration.AddKernelPersistence` extension with DbContext pooling (128 pool size).
  - `AppDbContextFactory` for design-time tooling (migrations).
  - Repository interfaces: `IAlbumRepository`, `IArtistRepository`, `ICustomerRepository`, `IEmployeeRepository`, `IGenreRepository`, `IInvoiceRepository`, `IInvoiceLineRepository`, `IMediaTypeRepository`, `IPlaylistRepository`, `ITrackRepository`.
- SharedKernel.DataSQLite project:
  - Concrete repository implementations for all entities above.
  - `BaseRepository<T>` with common CRUD operations.
- Host wiring (src/ModularMonolith.Api/Program.cs):
  - Auto-discovers SQLite file path from `src/ModularMonolith.Api/data/chinook.db` or repo root `/data/chinook.db`.
  - Sets `ConnectionStrings:AppDatabase` at runtime when not provided.
  - Registers all repository interfaces with their SQLite implementations.
  - Calls `AddKernelPersistence(builder.Configuration)`.
- Testing:
  - Integration tests use `WebApplicationFactory<Program>` with SQLite.
  - Repository unit tests in `tests/ModularMonolith.Api.Tests/Repositories/`.
- Target frameworks:
  - Individual csproj files specify `net10.0`, overriding `Directory.Build.props` which specifies `net9.0`.

Tips and commands
- Add a migration (from src/Shared/SharedKernel.Persistence):
  - dotnet ef migrations add <Name>
- Design-time override of the connection string for migrations:
  - Set environment variable ConnectionStrings__AppDatabase to an absolute SQLite Data Source before running dotnet ef.
  - Example (POSIX shells): export ConnectionStrings__AppDatabase="Data Source=/absolute/path/to/data/chinook.db"


Deliverables checklist
- [x] Create SharedKernel.Persistence project with AppDbContext, IAppDbContext, registration extensions, and design‑time factory.
- [x] Add EF Core packages and Sqlite provider.
- [x] Host registers persistence via AddKernelPersistence and configures absolute path to /data/chinook.db.
- [x] Repository pattern implemented (interfaces in SharedKernel.Persistence, implementations in SharedKernel.DataSQLite).
- [ ] Migrations are created and applied in Dev/Test as appropriate. (Using existing Chinook schema; no custom migrations yet.)
- [x] Modules depend only on SharedKernel (+ SharedKernel.Persistence for IAppDbContext) and use it via DI.
