# 0012. Give the shared kernel a budget, and fence its dependencies

- Status: Accepted
- Date: 2026-10-01
- Phase: 4 (see `docs/upgrade-plan.md`)

## Context

Phases 0 to 2 took the domain out of the shared kernel. Phase 3 added event
and outbox plumbing to it. A shared kernel grows quietly: each addition
looks reasonable, and nobody sees the total. Contracts projects have the
same failure mode: a "helper" with logic turns a contract into a shared
library.

## Decision

- `SharedKernel` may export at most 30 public types: its count after
  phase 3 (28) plus 2. `FenceTests.SharedKernel_Public_Surface_Stays_Within_Budget`
  enforces it. Raising the number means editing the test, which a reviewer
  sees in the diff.
- `SharedKernel` references no FluentValidation, module or Contracts
  assembly (`FenceTests`, `SharedKernelDependencyTests`).
- **Npgsql exception.** The plan's phase 4 rule says SharedKernel depends on
  no Npgsql, but its phase 2 put `ModuleDbContextOptions` (which calls
  `UseNpgsql` and opens an `NpgsqlConnection` for the data-health probe)
  in SharedKernel. Rather than add a project, the dependency is confined:
  only types in the `SharedKernel.Persistence` namespace (one file) may use
  Npgsql (`FenceTests.SharedKernel_Uses_Npgsql_Only_From_Its_Persistence_Namespace`).
  Changing the database provider touches that one file. The outbox and inbox
  SQL (`FOR UPDATE SKIP LOCKED`, `ON CONFLICT DO NOTHING`) is
  PostgreSQL-dialect text but uses no Npgsql type.
- Contracts projects contain only interfaces, enums, records (with no
  methods beyond the compiler's) and static classes of constants, and grant
  no `InternalsVisibleTo`.
- Anything a module registers against a SharedKernel interface that other
  modules also implement (today `IIntegrationEventHandler<T>`) is a keyed
  service, keyed by the module (its schema name). Nothing resolves it
  unkeyed, and a module's key yields only that module's implementations
  (`GuardrailTests.Integration_Event_Handlers_Resolve_Only_Within_Their_Own_Module_Key`).

## Consistency

Not a boundary decision.

## Consequences

- Growth of the kernel is visible and deliberate.
- Two more public types fit before the next conversation.
- The Npgsql exception is narrower than "no Npgsql" and has to be defended
  in review if it grows beyond `SharedKernel.Persistence`.
- While adding these rules we found that every ArchUnit dependency rule had
  been checking an empty set since before phase 0 (they selected assemblies
  by short name, which ArchUnitNET never matches, and allowed empty
  results). They now select by full name and require results; with real
  subjects they all still pass.

## How to reverse

Delete the budget and dependency tests. To remove the Npgsql exception
instead, move `ModuleDbContextOptions` into a separate provider project
referenced by the modules and the host.
