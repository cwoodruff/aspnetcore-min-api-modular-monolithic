# 0017. Catalog.Host reads Orders' outbox as a log, by cursor

- Status: Accepted
- Date: 2026-10-01
- Phase: 9 (see `docs/upgrade-plan.md`)

## Context

Phase 9 adds `samples/Catalog.Host`: Catalog running in a process of its own,
against the same database, consuming `InvoiceFinalized` (ADR-0009). Orders'
outbox (ADR-0008) is delivered by `OrdersOutboxDispatcher`, which claims rows
with `FOR UPDATE SKIP LOCKED` and marks a row processed once every handler
registered *in its process* has succeeded. A second process running the same
dispatcher would claim some rows, run only Catalog's handler, and mark them
processed; Administration's purchase summary (ADR-0010) would silently miss
those invoices.

## Decision

- `Catalog.Host` never writes to `orders."OutboxMessage"`. `OrdersOutboxFeed`
  reads it as an append-only log, in (`OccurredAt`, `Id`) order, past a cursor
  kept in `catalog_host.orders_outbox_cursor` (one row, created by the feed).
- Each event goes through Catalog's own subscriptions and `InboxGuard`, under
  the same handler name as in the monolith. Catalog's inbox therefore holds
  one entry per event whichever process handled it first, and running the
  sample next to the monolith counts each invoice once.
- Only rows older than `CatalogHost:Feed:SettleSeconds` (default 5) are read.
  `OccurredAt` is stamped before Orders' transaction commits, so a row can
  become visible behind a cursor that has already moved past its timestamp;
  the window must be longer than Orders' longest publishing transaction.
- A failing handler stops the batch at that row; the cursor stays before it
  and the next poll retries. There is no backoff or dead-lettering in the
  sample.
- Events Catalog does not subscribe to are passed over.

## Consistency

Outbox, eventual: unchanged from ADR-0009. Orders commits and writes the
outbox row in one transaction; Catalog catches up on its next poll after the
settle window. A caller may not assume track sales reflect an invoice
finalized moments ago, in either process.

## Consequences

- The sample proves Catalog's consumer needs nothing from Orders but the
  table's shape and `Orders.Contracts`; it does not prove a network transport.
- The table is now read by two consumers with different progress, which is
  what a broker topic gives for free. Orders' row retention must not delete
  rows the cursor has not reached (today rows are never deleted).
- The settle window is a guess about transaction length. A transaction that
  outlives it loses that event for the sample's consumer; the monolith's
  dispatcher, which reads by `ProcessedAt IS NULL`, is not affected.
- `catalog_host` is a schema no migration owns. It is created by the sample
  and is dropped with it.

## How to reverse

Delete `samples/Catalog.Host` and drop the `catalog_host` schema. Nothing in
`src/` depends on either.
