### EF Core in the modular monolith: one DbContext per module

**Status: Implemented (phase 2).** Decisions: [ADR-0002](adr/0002-database-engine.md)
(PostgreSQL), [ADR-0003](adr/0003-one-dbcontext-per-module.md) (a context per
module), [ADR-0004](adr/0004-orders-to-administration-customer.md) to
[ADR-0007](adr/0007-catalog-to-administration-mediatype.md) (the cross-module
references).

## Shape

| Module | Context | Schema | Entities |
|---|---|---|---|
| Catalog | `CatalogDbContext` | `catalog` | Artist, Album, Track, Playlist, PlaylistTrack |
| Orders | `OrdersDbContext` | `orders` | Invoice, InvoiceLine |
| Administration | `AdministrationDbContext` | `administration` | Customer, Employee, Genre, MediaType |
| Identity | none | | |
| Reporting | `ReportingDbContext` | `reporting` | IntegrityFinding; keyless views `sales_by_genre`, `invoice_lines_with_names` over the other schemas (ADR-0014) |

Each module keeps, all `internal`:

```
<Module>.Module/
  Domain/        entities (no base class; each declares its Id)
  Data/          <Module>DbContext, <Module>DbContextFactory (design time), Migrations/
  Models/        API models returned by endpoints
  Mapping/       <Module>Mappings: ToApiModel, ToEntity, ToApiModels
  Validation/    FluentValidation validators
  Services/      services that query the context directly
```

There is no repository layer and no shared persistence project. EF Core is the
repository: services query their module's context, reads use `AsNoTracking`,
and projections that used to live in repositories are private `Load*Async`
methods in the service that needs them.

## Registration

Each module registers its own context and validators in `RegisterServices`:

```csharp
services.AddModuleDbContext<CatalogDbContext>(CatalogDbContext.Schema);
services.AddValidatorsFromAssemblyContaining<AlbumValidator>(includeInternalTypes: true);
```

`AddModuleDbContext` (in `SharedKernel/Persistence/ModuleDbContextOptions.cs`)
does the only provider setup in the solution:

- `AddDbContextPool<T>` (pool size 128) against `ConnectionStrings:AppDatabase`,
  read from the built host's configuration so tests can override it;
- `UseNpgsql` with `MigrationsHistoryTable("__EFMigrationsHistory", schema)`;
- a second registration as `DbContext`, so the host can migrate every module's
  context without seeing its internal type.

Each context sets `HasDefaultSchema(Schema)` and calls
`ModuleDbContextOptions.UseUtcDateTimes` in `ConfigureConventions`: Npgsql only
writes UTC values to `timestamp with time zone`, and JSON dates without a zone
bind as `Unspecified`, so they are treated as UTC.

## Cross-module references

No navigation property and no foreign key crosses a module line. These columns
are plain ids with an index (for the `by-customer`, `by-track`, `by-genre` and
`by-media-type` lookups):

| Column | Refers to | ADR |
|---|---|---|
| `orders.Invoice.CustomerId` | `administration.Customer` | 0004 |
| `orders.InvoiceLine.TrackId` | `catalog.Track` | 0005 |
| `catalog.Track.GenreId` | `administration.Genre` | 0006 |
| `catalog.Track.MediaTypeId` | `administration.MediaType` | 0007 |

Validators require these ids to be present; nothing yet checks that the row
exists. Phase 3 adds read contracts in the owning module's `*.Contracts`
project, and phase 6 adds an orphan check. Within a module, relationships and
foreign keys stay as they were (Album to Artist, Track to Album, Playlist to
Track, InvoiceLine to Invoice, Customer to Employee, Employee to Employee).

`ModuleBoundaryTests.Each_Module_DbContext_Maps_Only_Its_Own_Entities` fails
if a context's model contains an entity type from another assembly.

## Outbox and inbox tables (phase 3)

Each module maps the shared outbox/inbox types from `SharedKernel/Events` into
its own schema ([ADR-0008](adr/0008-integration-events-and-outbox.md)):

| Table | Module | Purpose |
|---|---|---|
| `orders.OutboxMessage` | Orders (`modelBuilder.AddOutbox()`) | Events written with the business change; polled by `OrdersOutboxDispatcher` |
| `catalog.InboxMessage` | Catalog (`modelBuilder.AddInbox()`) | One row per (event, handler) already applied |
| `administration.InboxMessage` | Administration (`modelBuilder.AddInbox()`) | Same |
| `catalog.TrackSales` | Catalog | Read model fed by `InvoiceFinalized` (ADR-0009) |
| `administration.CustomerPurchaseSummary` | Administration | Read model fed by `InvoiceFinalized` (ADR-0010) |

`OutboxMessage` and `InboxMessage` are the only entity types a module context
may map from outside its own assembly; `ModuleBoundaryTests` allows them and
nothing else.

## Migrations

Each module's migrations live in its `Data/Migrations` folder and record
themselves in `<schema>.__EFMigrationsHistory`. The tool is pinned in
`dotnet-tools.json`:

```
dotnet tool restore
dotnet ef migrations add <Name> --project src/Modules/Catalog/Catalog.Module --context CatalogDbContext --output-dir Data/Migrations
dotnet ef migrations list --project src/Modules/Orders/Orders.Module --context OrdersDbContext
dotnet ef migrations script --project src/Modules/Administration/Admin.Module --context AdministrationDbContext
```

The design-time factories read `ConnectionStrings__AppDatabase` and fall back
to the docker-compose database. Generated migration classes are made
`internal` so the module assemblies keep exporting only their composition
class (`PublicSurfaceTests`).

Each context's first migration is `InitialSchema`. Together they produce the
phase 1 schema minus the four cross-schema foreign keys, plus one history
table per schema (checked with `pg_dump --schema-only`). A phase 1 database
cannot be upgraded in place because its history table was in `public`; local
volumes are recreated with `docker compose down -v`.

## Startup and seeding

In Development and Test, when `Database:MigrateAndSeedOnStartup` is true, the
host's `DbSeeder` (`src/ModularMonolith.Api/DbSeeder.cs`):

1. resolves every module context registered as `DbContext`;
2. migrates them in order: Administration, Catalog, Orders, Reporting (whose
   views read the other three), and fails on a context with no place in that
   order. Reporting's keyed context uses the owner connection for this; its
   services use `ConnectionStrings:Reporting`, a login in the read-only
   `reporting_reader` role the migration creates and grants;
3. loads `data/chinook-postgres-seed.sql` in one transaction if
   `catalog."Track"` is empty.

Other environments apply migrations as a deployment step.

## Health checks

Catalog, Orders and Administration report `connected` from their own
context's `Database.CanConnectAsync` (Reporting's as its read-only login). Identity owns no tables
yet; its data-health endpoint calls `ModuleDbContextOptions.CanConnectAsync`,
which only opens a connection.

## Tests

- `ModularMonolith.Module.Tests` runs each module on a host of its own
  (ADR-0016). Each host's database is cloned from a template that holds only
  that module's migrated schema (Reporting's has all four, for its views),
  optionally with its share of the Chinook seed (`ChinookSeed.For(schemas)`).
  Service tests reset the schema with Respawn (keeping the history table) and
  `TestData` writes a small graph through the module's own context.
- `ModularMonolith.Api.Tests` builds one seeded template database through
  `HostComposition` and `DbSeeder`, and gives every test host its own
  `CREATE DATABASE ... TEMPLATE` clone.
