# 0003. Give each module its own DbContext, schema and migration history

- Status: Accepted
- Date: 2026-09-30
- Phase: 2 (see `docs/upgrade-plan.md`)

## Context

Through phase 1 one `AppDbContext` in `SharedKernel.Persistence` mapped all
eleven entities, with navigation properties that crossed module lines, and
`SharedKernel.DataSQLite` held a repository per entity. Every module could
reach every table, and the shared kernel owned the domain. Phase 1 had already
put each table in its owner's schema (ADR-0002), so the split moves code, not
data.

## Decision

- Catalog, Orders and Administration each own a `DbContext`
  (`CatalogDbContext`, `OrdersDbContext`, `AdministrationDbContext`) that maps
  only their own entities (ADR-0001), with `HasDefaultSchema(<schema>)` and a
  migration history table in that schema (`<schema>.__EFMigrationsHistory`).
- Each module registers its context in `RegisterServices` through
  `SharedKernel/Persistence/ModuleDbContextOptions`, against the one shared
  connection string `ConnectionStrings:AppDatabase`. The host registers no
  context and no repository.
- Entities, API models, validators and mappers move into their module as
  `internal` types. The repository layer is dissolved: services use their
  module's context directly. EF Core is the repository.
- Each context has its own `InitialSchema` migration. Its generated schema
  equals the phase 1 schema minus the four cross-schema foreign keys
  (ADR-0004 to ADR-0007), plus the per-schema history tables; verified with
  `pg_dump --schema-only`.
- In Development and Test the host migrates Administration, Catalog, then
  Orders (Reporting joins in phase 6), then loads the seed.
- `SharedKernel.Persistence` and `SharedKernel.DataSQLite` are deleted.

## Consistency

Not a boundary decision in itself; it is what makes each boundary explicit.
A write now touches one module's context and commits one module's tables.
Nothing shares a transaction across modules any more. Each cross-module
reference has its own ADR.

## Consequences

- A module cannot query another module's tables: it has no `DbSet` for them,
  and `ModuleBoundaryTests` fails if a context maps another assembly's entity.
- Each module's migrations evolve independently and can be extracted with the
  module.
- Queries that used to join across modules can no longer do so. Responses that
  embedded another module's data now carry ids only (customer on an invoice,
  track names on invoice lines, genre and media type names on tracks,
  invoices on a customer).
- A phase 1 database cannot be upgraded in place, because the history tables
  moved. Local volumes are recreated once (`docker compose down -v`); there is
  no production data yet.
- Services.Tests now run against PostgreSQL with each module's real context,
  because there is no repository left to substitute.

## How to reverse

Recreate a shared context that maps every module's entities (they would have
to become visible to it again), merge the three migration histories into one,
and move the repositories back. The per-module schemas can stay as they are.
