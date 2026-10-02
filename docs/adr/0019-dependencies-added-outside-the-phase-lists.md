# 0019. Dependencies added during the upgrade that no phase listed

- Status: Accepted
- Date: 2026-10-02
- Phase: after 9 (see `docs/upgrade-plan.md`, working agreement #3)

## Context

The upgrade plan lists the packages each phase may add and says anything else needs an ADR. The audit after
phase 9 compared every `PackageReference` and local tool with the baseline (commit 42199a3) and found
dependencies that no phase listed and no ADR recorded. All are Microsoft packages, at the versions the rest
of the solution already uses.

## Decision

These stay, for the reasons given:

| Dependency | Where | Why |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Relational` 10.0.11 | SharedKernel | Pins the relational layer to the EF Core version the modules use. Npgsql 10.0.3 alone resolves an older `Relational`, and the mismatch fails at runtime (found in phase 1). The baseline already referenced `Microsoft.EntityFrameworkCore`; this is its relational part, not a new library. |
| `Microsoft.AspNetCore.Mvc.Testing` 10.0.11 | Module.Tests | `WebApplicationFactory` for `samples/Catalog.Host` (phase 9); it also brings `Microsoft.AspNetCore.TestHost`, which `ModuleTestHost` uses. Already at baseline in Api.Tests. |
| `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 | samples/Catalog.Host | Validates the monolith's tokens (ADR-0020). Already at baseline in Identity.Module. |
| `dotnet-ef` 10.0.11 (local tool, `dotnet-tools.json`) | repo root | Per-module migrations (phase 2), pinned so every machine generates the same code. A tool, not a runtime dependency. |

Removed instead of recorded: an explicit `Microsoft.AspNetCore.TestHost` reference in Module.Tests, which
`Mvc.Testing` already brings.

## Consistency

Not a boundary decision.

## Consequences

- Every package in the solution is now either at baseline, allowed by a phase, or listed here.
- The `Relational` pin must move with EF Core: upgrading one without the other brings the phase 1 failure back.

## How to reverse

Remove the reference. For `Relational`, only once Npgsql resolves a matching version by itself; for the
others, drop the feature that needs them (the Catalog.Host tests, the sample, or generating migrations).
