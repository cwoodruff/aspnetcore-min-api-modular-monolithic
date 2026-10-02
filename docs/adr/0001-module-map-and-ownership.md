# 0001. Record the module map and entity ownership

- Status: Superseded by [0018](0018-module-map-after-the-upgrade.md)
- Date: 2026-09-30
- Phase: 0 (see `docs/upgrade-plan.md`)

## Context

The host (`src/ModularMonolith.Api/Program.cs`) composes five modules from an
explicit list. Each implements `SharedKernel.IModule` and maps its endpoints
under its own route group. Persistence is not yet split: one `AppDbContext`
in `SharedKernel.Persistence` holds all eleven entity types, and
`SharedKernel.DataSQLite` holds every repository. Ownership is therefore a
convention, not something the compiler or the database enforces. Later phases
move each entity into its owner's module and schema; they need one written
answer to "who owns this table".

## Decision

Five modules, each owning the entities listed. Only the owner writes an
entity. Other modules reach it through the owner's public contract (from
phase 2 onward, its `*.Contracts` project).

| Module | Assembly | Route group | Owns |
|---|---|---|---|
| Catalog | `Catalog.Module` | `/api/catalog` | Artist, Album, Track, Playlist, PlaylistTrack |
| Orders | `Orders.Module` | `/api/orders` | Invoice, InvoiceLine |
| Administration | `Admin.Module` | `/api/admin` | Customer, Employee, Genre, MediaType |
| Identity | `Identity.Module` | `/api/identity` | No persisted entities (users, refresh tokens and key material are held in memory or in configuration) |
| Reporting | `Reporting.Module` | `/api/reporting` | None (it will own a read model in phase 6) |

Cross-module references that exist today as EF navigations, and that later
phases replace with ID-only references:

- `Invoice.Customer` (Orders to Administration)
- `InvoiceLine.Track` (Orders to Catalog)
- `Track.Genre` and `Track.MediaType` (Catalog to Administration)

## Consistency

Today every cross-module write shares one `AppDbContext` and one transaction
by default. That is recorded here as the starting point, not endorsed. Each
reference above gets its own ADR with an explicit consistency choice when it
is cut (phase 2).

## Consequences

- Reviewers have one table to check ownership against.
- `GuardrailTests` enforce the parts that can be checked today: one
  `InternalsVisibleTo` per module at most, no module service resolving to
  another module's type, every referenced policy registered, unique routes.
- Nothing yet stops a module from querying another module's `DbSet` through
  the shared `AppDbContext`; phase 2 closes that.

## How to reverse

Edit this table in a superseding ADR. Moving an entity between modules means
moving its entity, validator, service and endpoints into the new owner and
updating every ADR that references it.
