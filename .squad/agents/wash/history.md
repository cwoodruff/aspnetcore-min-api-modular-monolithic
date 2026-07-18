# History - Uwash

## Project Context
- Project: aspnetcore-min-api-modular-monolithic (ASP.NET Core 10, Minimal APIs, modular monolith)
- Modules: Music, Orders, Admin, Reporting, Identity
- Stack: EF Core, FluentValidation, SQLite, xUnit, WebApplicationFactory, Docker
- User: Chris Woodruff
- Team formed: 2026-07-18T15:23:33-04:00

## 2026-07-18T15:48:10-04:00
- Fixed the NuGet audit build failure by upgrading `Swashbuckle.AspNetCore` from 10.1.5 to 10.2.3 in `src/ModularMonolith.Api/ModularMonolith.Api.csproj`.
- Added `SQLitePCLRaw.bundle_e_sqlite3` 3.0.4 overrides in `src/Shared/SharedKernel.Persistence/SharedKernel.Persistence.csproj` and `tests/ModularMonolith.Api.Tests/ModularMonolith.Api.Tests.csproj` to bypass vulnerable `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.
- Verified success with `dotnet build` and `dotnet test` (171/171 passing).
