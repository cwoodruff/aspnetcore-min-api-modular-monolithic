# 0018. Module map and entity ownership after the upgrade

- Status: Accepted
- Date: 2026-10-02
- Phase: after 9 (see `docs/upgrade-plan.md`)
- Supersedes: [0001](0001-module-map-and-ownership.md)

## Context

ADR-0001 recorded who owns which table before any code moved, and calls itself
the one table to check ownership against. Phases 2 to 6 gave every module its
own context and schema and added tables (ADR-0008 to ADR-0010, ADR-0014), each
recorded in its own ADR. ADR-0001's table no longer lists them, and its
context (one `AppDbContext`, repositories in `SharedKernel.DataSQLite`)
describes code that no longer exists.

## Decision

The ownership table, as the code stands. Only the owner writes a table; the
owner's `DbContext` maps it, in the owner's schema
(`ModuleBoundaryTests.Each_Module_DbContext_Maps_Only_Its_Own_Entities`).

| Module | Assembly | Schema | Route group | Owns |
|---|---|---|---|---|
| Catalog | `Catalog.Module` | `catalog` | `/api/catalog` | Artist, Album, Track, Playlist, PlaylistTrack; TrackSales (read model fed by `InvoiceFinalized`, ADR-0009); InboxMessage (ADR-0008) |
| Orders | `Orders.Module` | `orders` | `/api/orders` | Invoice (with Status), InvoiceLine; OutboxMessage (ADR-0008) |
| Administration | `Admin.Module` | `administration` | `/api/admin` | Customer, Employee, Genre, MediaType; CustomerPurchaseSummary (read model fed by `InvoiceFinalized`, ADR-0010); InboxMessage (ADR-0008) |
| Reporting | `Reporting.Module` | `reporting` | `/api/reporting` | IntegrityFinding (ADR-0015); views `sales_by_genre` and `invoice_lines_with_names`, which read the other three schemas as the `reporting_reader` role (ADR-0014) |
| Identity | `Identity.Module` | none | `/api/identity` | No persisted entities (users, refresh tokens and key material are held in memory or in configuration) |

`OutboxMessage` and `InboxMessage` are SharedKernel types each module maps
into its own schema; the rows belong to that module.

Cross-module references are ids with no foreign key: `Invoice.CustomerId`
(ADR-0004), `InvoiceLine.TrackId` (ADR-0005), `Track.GenreId` (ADR-0006),
`Track.MediaTypeId` (ADR-0007).

## Consistency

Not a boundary decision. Each reference and each read model keeps the
consistency choice its own ADR records.

## Consequences

- Reviewers again have one current table to check ownership against.
- A new table needs a row here, or a superseding ADR, in the same PR.

## How to reverse

Edit this table in a superseding ADR, as ADR-0001 says.
