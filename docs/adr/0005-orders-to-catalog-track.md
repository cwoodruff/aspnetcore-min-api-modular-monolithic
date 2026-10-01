# 0005. Orders refers to Catalog's Track by id only

- Status: Accepted
- Date: 2026-09-30
- Phase: 2 (see `docs/upgrade-plan.md`)

## Context

`orders.InvoiceLine.TrackId` points at `catalog.Track`. Until phase 2 it was
a foreign key with an `InvoiceLine.Track` navigation and a
`Track.InvoiceLines` inverse. Invoice responses joined in each line's
`TrackName`, and Catalog served `GET /api/catalog/tracks/invoice/{id}` by
reading Orders' invoice lines through the shared context.

## Decision

- Drop the navigation, its inverse and the foreign key
  `FK_InvoiceLine_Track_TrackId`. Keep `TrackId` as an indexed column.
- Invoice lines carry `TrackId` only; `TrackName` is gone from responses.
- Delete `GET /api/catalog/tracks/invoice/{id}`: it read another module's
  data. `GET /api/orders/invoice-lines/invoice/{id}` serves invoice lines.
- Phase 3 gives Orders a track read contract in `Catalog.Contracts` if names
  are needed, and publishes track sales as events rather than joins.

## Consistency

Application-validated id, no database foreign key, eventual. Today `InvoiceLineValidator` requires a non-null `TrackId`; nothing checks
that the track exists.

## Consequences

- An invoice line can name a track id that does not exist, and a track that
  has been sold can be deleted (no track delete endpoint exists today).
- A removed track leaves invoice lines with a dangling `TrackId`; the line's
  price and quantity are still correct because they are stored on the line.
  Phase 6's orphan check reports such rows.
- Clients that read `TrackName` from invoice lines, or called
  `/api/catalog/tracks/invoice/{id}`, must change.

## How to reverse

Re-add the navigation and foreign key in a context that maps both (undoing
ADR-0003 for Orders and Catalog), restore the endpoint, and clean up orphans
before the constraint is recreated.
