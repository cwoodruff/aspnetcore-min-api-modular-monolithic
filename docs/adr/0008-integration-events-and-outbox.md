# 0008. Hand-rolled integration events through a per-module outbox

- Status: Accepted
- Date: 2026-10-01
- Phase: 3 (see `docs/upgrade-plan.md`)

## Context

Until phase 3 nothing crossed a module boundary at write time; any
cross-module effect would have needed a shared transaction, which phase 2
removed on purpose (ADR-0003). Orders now needs Catalog and Administration
to react when an invoice is finalized, without a shared transaction and
without either module calling the other.

## Decision

Integration events, an outbox and an inbox, written by hand in
`SharedKernel/Events`, with no new packages.

- **Publishing.** `IEventPublisher.PublishAsync(event, sameTransactionAs, ct)`
  adds an `OutboxMessage` to the publishing module's own context. The row
  commits in the same `SaveChanges` as the business change or not at all.
  There is no in-memory publish path, so an event never exists for a change
  that rolled back.
- **Event contracts.** Event types live in the publisher's `*.Contracts`
  project (`Orders.Contracts/Events/InvoiceFinalized`). Consumers reference
  that project and never the publisher's module (architecture tests).
- **Storage.** Each publishing module has an `OutboxMessage` table and each
  consuming module an `InboxMessage` table, in its own schema, mapped with
  `AddOutbox()` / `AddInbox()`.
- **Dispatch.** One `OutboxDispatcher` per publishing module, a hosted
  service, polls its outbox: due, undelivered, not dead-lettered rows,
  oldest first, 50 at a time, `FOR UPDATE SKIP LOCKED` so a second instance
  skips rows already taken. It deserializes by event type name and hands the
  event to every registered `IIntegrationEventHandler<T>`, each in its own
  DI scope.
- **Delivery guarantee: at least once.** A row is marked processed only
  when every handler has succeeded. A crash between a handler committing and
  the row being marked delivers the event again.
- **Inbox per handler.** `InboxGuard` runs each handler on its own module's
  context: insert `(EventId, HandlerName)` into that module's inbox first
  (`ON CONFLICT DO NOTHING`); if the row already existed, skip; otherwise
  run the handler and commit its changes and the inbox row together. So a
  handler takes effect once per event, however often the event arrives, and
  a handler that succeeded is skipped when a sibling's failure causes a
  retry.
- **Retries.** A failed delivery is retried after 1 s, 5 s, 30 s, 2 min and
  10 min. The sixth failed delivery dead-letters the row: the dispatcher
  leaves it alone, and `GET /api/orders/outbox/dead-letters` (admin) lists
  it. `POST .../{id}/retry` resets its attempts so the next poll delivers it
  again. Nothing deletes or replays dead letters automatically; a person
  looks at them.
- **Ordering, stated narrowly.** Rows are delivered in insertion order
  (`OccurredAt`, then `Id`) within one dispatcher, and only until a row
  fails: a row waiting for a retry is delivered after rows written later.
  There is no ordering across modules or across dispatchers, and handlers
  must not depend on any. There is no sequence number or lock to enforce
  more; the handlers here are commutative counters, which do not need it.

Why not MediatR (or MassTransit): MediatR moved to a commercial licence, and
neither the in-process mediator nor a bus gives this repository what it
needs to show: the transaction boundary, the delivery guarantee, the retry
schedule and the dead-letter policy as code a reader can follow in one
folder. Owning roughly 300 lines is cheaper than owning someone else's
semantics.

## Consistency

Outbox, eventual. The publisher's change and its event commit together;
consumers catch up after the next poll (one second by default). ADR-0009 and
ADR-0010 record what each consumer's readers may and may not assume.

## Consequences

- Cross-module effects survive a consumer being down or failing; they wait
  in the outbox and are retried.
- Every handler must be safe to run more than once in principle; the inbox
  makes it run once in practice, but only for changes it makes on its own
  module's context through the guard.
- Handlers must not call `SaveChanges` themselves or touch another context.
- Someone has to watch the dead-letter list. Phase 5 wires the dispatcher's
  counters (processed, failed deliveries, dead-lettered) into module
  metrics.
- Outbox and inbox rows are never deleted yet; a cleanup job is future work.

## How to reverse

Replace `IEventPublisher` with a direct call into the consumers inside the
publisher's transaction (which brings back a shared transaction across
schemas) or with a message bus. Handlers, contracts and inbox tables can stay
as they are behind either.
