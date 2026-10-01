# 0002. Use PostgreSQL, with one schema per owning module

- Status: Accepted
- Date: 2026-09-30
- Phase: 1 (see `docs/upgrade-plan.md`)

## Context

Until phase 1 the application ran on a committed SQLite file
(`src/ModularMonolith.Api/data/chinook.db`), with no migrations: the schema was
whatever the file held. Repository tests ran on the EF Core in-memory provider,
which enforces no constraints and translates no SQL.

SQLite has no schemas. Everything later phases need in order to give each
module its own data depends on them: a schema per module, a migration history
table per `DbContext`, dropping cross-schema foreign keys, and an orphan check
across schemas (phases 2 and 6). None of it can be tried on SQLite.

## Decision

- PostgreSQL 17 is the only database engine. Local development runs it from
  `docker-compose.yml`; tests run it with Testcontainers.
- The schema is defined by EF Core migrations (`InitialSchema`, kept in
  `SharedKernel.Persistence/Migrations` until phase 2 splits the context).
- Every table is created in the schema of the module that owns it (ADR-0001):
  `catalog`, `orders`, `administration`. Identity and Reporting have no tables
  yet. Phase 2 then moves code, not data.
- Column types are Postgres-native: `varchar(n)`, `timestamp with time zone`,
  `numeric(10,2)`. Dates without a time zone are stored as UTC.
- Seed data is `data/chinook-postgres-seed.sql`, generated from the former
  SQLite file so the rows and `Id` values are unchanged. `DbSeeder` applies
  migrations and loads it in Development and Test only. Other environments
  apply migrations as a deployment step.
- There is no second provider. A zero-dependency demo mode would need its own
  ADR; carrying SQLite next to PostgreSQL would mean two dialects to test.

## Consistency

Not a boundary decision. All three schemas are still reached through one
`AppDbContext`, so cross-module writes still share a transaction, and foreign
keys still cross schemas (for example `orders.InvoiceLine` to `catalog.Track`).
Phase 2 records a consistency choice for each of those references and removes
the foreign keys.

## Consequences

- Running the app needs Docker, or a reachable PostgreSQL set in
  `ConnectionStrings:AppDatabase`. A fresh clone no longer runs on its own.
- Tests need Docker. Each test host gets its own clone of a seeded template
  database (`CREATE DATABASE ... TEMPLATE`), so test classes still run in
  parallel. Repository tests share one empty database, reset with Respawn.
- Repository tests now run on the real engine, so constraints, SQL
  translation and identity sequences are actually tested.
- Postgres string comparison is case-sensitive, unlike the old SQLite `LIKE`.

## How to reverse

Swap `UseNpgsql` in `PersistenceRegistration.UseAppDatabase` for another
provider, regenerate the migration, regenerate the seed script for that
dialect, and replace `PostgresFixture`. If phase 2 has merged, the per-module
schemas have to be flattened or emulated as well.
