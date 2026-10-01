# 0016. Test each module on a host of its own

- Status: Accepted
- Date: 2026-10-01
- Phase: 8 (see `docs/upgrade-plan.md`)

## Context

Until phase 8 every endpoint test booted the whole application through
`WebApplicationFactory<Program>`: all five modules, Identity's login, and a
clone of the full seeded database. A Catalog endpoint test therefore depended
on Orders, Administration and Identity starting cleanly, and paid for them.
`ModularMonolith.Services.Tests` ran services on PostgreSQL but migrated all
three module schemas into one database, and its outbox tests needed all three
modules registered at once (`ModuleHost`). Nothing showed that a module could
run without the others.

## Decision

`tests/ModularMonolith.Module.Tests` replaces Services.Tests.
`ModuleTestHost<TModule>` (in `Hosting/`) builds a `WebApplication` on
`TestServer` with:

- what `HostComposition` gives every module (problem details, JSON options,
  reflection-based serialization) and the one module's `RegisterServices` and
  `MapEndpoints`;
- a database cloned from a per-module template that holds only that module's
  schema, optionally with its share of the Chinook seed. Respawn resets only
  that schema. Reporting is the exception: its views read the other three
  schemas, so its host migrates all four (ADR-0014);
- a header-based authentication scheme and the policies `Identity.Contracts`
  names, with Identity's tenant rule, in place of the Identity module.
  Identity's own host uses its real authentication;
- a recording wrapper around the module's `IEventPublisher` (`Published`),
  and `DeliverAsync`, which runs the module's event subscriptions through its
  inbox as the publisher's dispatcher would. The outbox loop and the
  integrity job are off.

Outbox delivery (retries, backoff, dead letters, idempotence) is tested once,
in `Kernel/OutboxDispatcherTests`, with test contexts. Each module's side of
`InvoiceFinalized` is tested in its own host. `ModularMonolith.Api.Tests`
keeps what needs the whole host: the finalized-invoice flow across three
modules, the authorization pipeline, rate limits across modules, health,
security headers, error handling, endpoint filters, OpenAPI, seed integrity,
and Identity (whose tests replace its services inside the full host, so its
`InternalsVisibleTo` stays with Api.Tests).

## Consistency

Not a boundary decision.

## Consequences

- A module that cannot start without another module's services fails its own
  tests, not only `GuardrailTests`.
- Endpoint tests own their database, so they assert exact statuses and seeded
  values instead of "not 401" or "404 or 500".
- Per test, the module host costs about a quarter of the full host (README,
  "Test cost").
- The fake policies in `TestAuth` mirror Identity's rules. If Identity changes
  a rule, `TestAuth` must change with it; Api.Tests' authorization tests are
  what catch a drift.

## How to reverse

Move the endpoint tests back into Api.Tests on `ApiFactory`, rename
Module.Tests back to Services.Tests, and point the `InternalsVisibleTo`
grants and `ArchitectureConstants` at it.
