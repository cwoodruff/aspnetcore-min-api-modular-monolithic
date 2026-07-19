# History - Uzoe

## Project Context
- Project: aspnetcore-min-api-modular-monolithic (ASP.NET Core 10, Minimal APIs, modular monolith)
- Modules: Music, Orders, Admin, Reporting, Identity
- Stack: EF Core, FluentValidation, SQLite, xUnit, WebApplicationFactory, Docker
- User: Chris Woodruff
- Team formed: 2026-07-18T15:23:33-04:00

## 2026-07-18T15:28:34-04:00
- Completed full solution review of `ModularMonolith.Api` in sync mode.
- Top findings: hardcoded demo credentials, ephemeral JWT signing keys, validation failures surfacing as 500s, leaky module boundaries, and build restore blocked by NuGet vulnerability audit warnings.
- Additional findings: service tests excluded from the solution, public operational metadata exposed by default endpoints, plus docs/config drift.

## 2026-07-18T16:08:19-04:00
- Tightened module boundaries by changing implementation types to `internal` across Music, Orders, Administration, Reporting, and Identity while preserving public composition entry points.
- Added `InternalsVisibleTo` only for legitimate test projects and introduced `tests/ModularMonolith.Architecture.Tests/PublicSurfaceTests.cs` to guard exported module surfaces.
- Validation finished cleanly with `dotnet build` and `dotnet test` passing (179/179).

## 2026-07-19T09:01:58-04:00
- Took over a lead escalation after two earlier auth fixes did not fully resolve Chris's persistent local 403 on `/api/admin/customers`.
- Verified the auth pipeline end-to-end, confirmed the remaining live failure was stale split local `Identity:InMemoryUsers` config rather than a claim-type or policy mismatch, and added startup logging to surface the effective loaded claims.
- Fixed missing `role.admin` enforcement across admin endpoints and finished with `dotnet build` plus `dotnet test` passing (244/244).

## 2026-07-19T17:52:55-04:00
- Refreshed `README.md` to align documentation with the current codebase after the team's auth, boundary, testing, and operational fixes.
- Captured the remaining Dockerfile/`Directory.Build.props` `net9.0` vs `net10.0` drift as a documentation note only; no code changes were made in this pass.
