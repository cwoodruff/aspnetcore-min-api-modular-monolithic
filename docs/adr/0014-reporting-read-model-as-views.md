# 0014. Reporting reads across modules through views, as a read-only role

- Status: Accepted
- Date: 2026-10-01
- Phase: 6 (see `docs/upgrade-plan.md`)

## Context

Reporting exists to answer questions that span modules: sales per genre
(Catalog's tracks and sales, Administration's genres), invoice lines with
track and customer names (Orders, Catalog, Administration). Every other
cross-module need in this repo goes through events (ADR-0008) or ids
(ADR-0004 to ADR-0007). Doing that for reporting would mean Reporting keeping
copies of most of the other modules' data in sync by events: a lot of code
for a module that only reads.

## Decision

- Reporting owns schema `reporting` with its own `ReportingDbContext` and
  migration history, migrated last (after administration, catalog and
  orders).
- Its read model is two PostgreSQL views in that schema, created by raw SQL
  in its migration and mapped as keyless entities:
  - `reporting.sales_by_genre`: genre, track count, units sold
    (`administration.Genre`, `catalog.Track`, `catalog.TrackSales`);
  - `reporting.invoice_lines_with_names`: invoice lines with track and
    customer names (`orders.InvoiceLine`, `orders.Invoice`,
    `catalog.Track`, `administration.Customer`).
- **This is coupling, on purpose.** Reporting depends on other modules'
  table and column names. That is the trade the video describes: reporting
  reads the other modules' stores directly instead of each module exporting
  reporting data. It is acceptable here because:
  - it is read-only, enforced by the database, not by convention:
    Reporting connects as the `reporting_reader` role, which has `SELECT` on
    the three schemas and write access only to
    `reporting.IntegrityFinding`. The migration creates the role and the
    grants; logins in it are provided per environment (`ConnectionStrings:Reporting`;
    the compose init script creates `reporting` locally);
  - it is one-directional: no module depends on Reporting, and Reporting
    has no Contracts project;
  - a rename breaks Reporting's tests first: every table and column the
    views and checks name is listed in `ReportingSql.References`, and a test
    checks each against the owning module's current EF model;
  - PostgreSQL also refuses to drop or retype a column a view uses, so a
    breaking change in another module fails its migration loudly rather
    than corrupting reports.
- Migrations run as the schema owner (`ConnectionStrings:AppDatabase`), never
  as the reader. The view SQL is versioned (`CreateSalesByGenreV1`); changing
  a view means adding V2 and a migration that replaces it.
- Report queries scan other modules' tables, so each runs inside Reporting's
  `ModuleGate` (default 4 at a time, ADR-0013) and its rate-limit policy.

## Consistency

Read only. Views read the owners' tables at query time, so they are as
current as the data they read: `invoice_lines_with_names` is immediately
consistent; `sales_by_genre` is as eventually consistent as `TrackSales`
(ADR-0009).

## Consequences

- Reporting answers cross-module questions without any module exporting
  data for it, and without copies to keep in sync.
- Other modules cannot freely rename or drop the columns listed in
  `ReportingSql.References`; the rename-detection test says which.
- Database-level coupling makes extracting Catalog, Orders or
  Administration to their own database harder: Reporting would then need
  replicated data or events. That is phase 9's problem, and this ADR is
  where to start.
- Creating the role needs `CREATEROLE` for whoever runs migrations.

## How to reverse

Replace the views with tables Reporting fills from integration events
(ADR-0008), drop the role's `SELECT` grants on the other schemas, and delete
the rename-detection references.
