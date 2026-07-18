# Squad Decisions

## Active Decisions

### 2026-07-18T15:28:34-04:00: Review finding
**By:** Zoe
**What:** Default solution build currently fails restore because NuGet vulnerability audit warnings are treated as errors for Microsoft.OpenApi 2.4.1 and SQLitePCLRaw.lib.e_sqlite3 2.1.11.
**Why:** The documented build path is broken out of the box, and the dependency versions need remediation before the project can build cleanly in normal CI settings.
**Status:** RESOLVED by `2026-07-18T15:48:10-04:00: NuGet audit remediation` below.

### 2026-07-18T15:28:34-04:00: Review finding
**By:** Zoe
**What:** JWT signing keys are generated in memory at startup and the configured KeyProvider/KeyVault settings are not used.
**Why:** Access tokens become invalid after every restart and multi-instance deployments cannot share trust material, creating both availability and operational security problems.
**Status:** RESOLVED by `2026-07-18T15:51:51-04:00: Persist JWT signing keys and wire KeyVault provider` below.

### 2026-07-18T15:28:34-04:00: Review finding
**By:** Zoe
**What:** Identity registers an in-memory demo user store with hard-coded usernames/passwords and uses it unconditionally for login.
**Why:** This exposes known credentials (including an admin account) anywhere the app runs unless the registration is replaced, making the starter unsafe to deploy beyond local demos.
**Status:** RESOLVED by `2026-07-18T15:51:51-04:00: Lock down development-only in-memory identity users` below.

### 2026-07-18T15:28:34-04:00: Review finding
**By:** Zoe
**What:** Modules expose many public services and endpoint classes instead of keeping everything internal except the IModule entry point.
**Why:** This weakens the modular-monolith boundary, enlarges the public API surface between assemblies, and makes accidental cross-module coupling easier over time.

### 2026-07-18T15:28:34-04:00: Review finding
**By:** Zoe
**What:** Swagger and health/data-health endpoints are exposed in all environments and return environment, version, and database connectivity metadata without authentication.
**Why:** This increases public reconnaissance value and may disclose operational details that are useful to attackers or inappropriate for internet-facing deployments.

### 2026-07-18T15:28:34-04:00: Review finding
**By:** Zoe
**What:** The service test project is not included in the solution, and running it directly currently fails to compile because service constructors now require ILogger dependencies.
**Why:** A whole layer of tests is silently skipped by normal solution test runs, leaving service behavior and future refactors less protected than the repository suggests.

### 2026-07-18T15:28:34-04:00: Review finding
**By:** Zoe
**What:** Module services throw FluentValidation.ValidationException, but the host only wires generic exception handling and does not translate validation failures into structured 400 responses.
**Why:** Invalid client input can bubble out as 500s, which breaks API contracts, confuses clients, and weakens observability around user-caused versus server-caused failures.

### 2026-07-18T15:48:10-04:00: NuGet audit remediation
**By:** Wash
**What:** Upgraded `src/ModularMonolith.Api/ModularMonolith.Api.csproj` from `Swashbuckle.AspNetCore` 10.1.5 to 10.2.3 so restore resolves `Microsoft.OpenApi` 2.7.5 instead of vulnerable 2.4.1, and added direct `SQLitePCLRaw.bundle_e_sqlite3` 3.0.4 references in `src/Shared/SharedKernel.Persistence/SharedKernel.Persistence.csproj` and `tests/ModularMonolith.Api.Tests/ModularMonolith.Api.Tests.csproj` so restore resolves the non-vulnerable SQLite bundle instead of `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.
**Why:** Default `dotnet build ModularMonolith.Api.sln` was failing with NU1903 for `Microsoft.OpenApi` 2.4.1 (GHSA-v5pm-xwqc-g5wc; patched in 2.7.5+) and `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (GHSA-2m69-gcr7-jv3q / CVE-2025-6965). `Microsoft.EntityFrameworkCore.Sqlite` 10.0.10 still depends on the vulnerable 2.1.11 bundle upstream, so the safest fix here was a direct package override to `SQLitePCLRaw.bundle_e_sqlite3` 3.0.4 rather than suppressing NuGet audit warnings. Resolves Zoe's `2026-07-18T15:28:34-04:00: Review finding` about the default build being blocked by vulnerability audit warnings.

### 2026-07-18T15:51:51-04:00: Lock down development-only in-memory identity users
**By:** Kaylee
**What:** Reworked `src/Modules/Identity/Identity.Module/Services/InMemoryStores.cs` so in-memory users come from `Identity:InMemoryUsers` configuration instead of hard-coded passwords, changed `src/Modules/Identity/Identity.Module/Extensions/IdentityAuthExtensions.cs` to register the in-memory store only for Development/Demo and a disabled store elsewhere, updated local-dev configuration/docs in `src/ModularMonolith.Api/ModularMonolith.Api.csproj`, `src/ModularMonolith.Api/appsettings.Development.json`, and `README.md`, scrubbed hard-coded demo passwords from docs/workshop content, and updated identity tests in `tests/ModularMonolith.Api.Tests/TestAuthHelpers.cs` plus `tests/ModularMonolith.Api.Tests/IdentityEndpointsTests.cs`.
**Why:** Zoe flagged the always-on in-memory login with baked-in demo/admin credentials as a critical deployment risk in `.squad/decisions.md`. This change removes known passwords from source and ensures non-development environments do not expose a default login path with seeded credentials.

### 2026-07-18T15:51:51-04:00: Persist JWT signing keys and wire KeyVault provider
**By:** Inara
**What:** Updated `src/Modules/Identity/Identity.Module/Extensions/IdentityAuthExtensions.cs` to select key providers by environment/config, enhanced `src/Modules/Identity/Identity.Module/KeyManagement/DevKeyMaterialService.cs` to persist a generated RSA key to disk with safe file locking/atomic writes, added `src/Modules/Identity/Identity.Module/KeyManagement/KeyVaultKeyMaterialService.cs` for Azure Key Vault-backed signing/JWKS, added Azure Key Vault package references in `src/Modules/Identity/Identity.Module/Identity.Module.csproj`, documented dev/prod key configuration in `src/ModularMonolith.Api/appsettings.Development.json` and `README.md`, and added restart/gating coverage in `tests/ModularMonolith.Api.Tests/IdentityEndpointsTests.cs`.
**Why:** Zoe's review found JWT signing keys were generated only in memory and the existing `Jwt:KeyProvider` / Key Vault configuration was ignored, which invalidated tokens on restart and made shared deployment trust material impossible. This change makes development keys survive restarts while wiring the existing external-provider config path so non-development environments must use a real signing key source.

### 2026-07-18T16:05:29-04:00: Expanded identity user account documentation
**By:** Kaylee
**What:** Expanded `README.md` documentation for the Identity module's development-only in-memory user accounts, including the account field model, authorization policy mapping, fuller user-secrets examples, multi-user array indexing, and the non-production security model. Added this decision record in `.squad/decisions/inbox/kaylee-user-account-model-docs.md`.
**Why:** Chris requested clearer user account documentation after the recent identity security fixes removed hard-coded demo credentials and moved demo users to configuration.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
