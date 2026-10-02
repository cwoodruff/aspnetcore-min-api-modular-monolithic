# 0020. Catalog.Host validates Identity's tokens over HTTP and keeps its own policy copies

- Status: Accepted
- Date: 2026-10-02
- Phase: 9 (see `docs/upgrade-plan.md`)

## Context

Inside the monolith, Identity issues and validates tokens and registers the authorization policies whose
names every module applies (ADR-0011). `samples/Catalog.Host` runs Catalog without Identity.Module
(ADR-0017), yet Catalog's endpoints still require those policies. The sample must accept the monolith's
tokens without referencing Identity.Module, or the "builds from Catalog and SharedKernel alone" check it
exists for would be false.

## Decision

- `samples/Catalog.Host/RemoteIdentity.cs` validates JWTs with `JwtBearer`, reading the signing keys from
  Identity's published key set (`CatalogHost:IdentityJwksUrl`, by default
  `http://localhost:5043/api/identity/.well-known/jwks.json`) through a `ConfigurationManager` that caches
  and refreshes them. Issuer and audience come from `Jwt:Issuer` and `Jwt:Audience`, with Identity's
  defaults.
- It registers a policy for every `Identity.Contracts.Permissions` constant, `Policies.Admin` and
  `Policies.TenantScoped`, with the rules Identity's `PolicyRegistry` and `TenantAuthorizationHandler` apply:
  the `permissions` claim, the Admin role, and a tenant claim that must match `X-Tenant-Id` when sent.
- Catalog.Host references no Identity assembly besides `Identity.Contracts`, which comes through
  Catalog.Module (`ExtractionReadinessTests`).

## Consistency

Read only. Catalog.Host reads Identity's public keys over HTTP and never writes anything Identity owns. A
key Identity rotates in is picked up when the cached key set refreshes; until then a token signed with it
is rejected.

## Consequences

- The policy rules now exist in two places outside Identity: here and in Module.Tests' `TestAuth`. A change
  to an Identity rule must be copied to both; Api.Tests' authorization tests cover only Identity's own copy.
- Catalog.Host cannot authenticate anyone while the monolith's key set is unreachable. The sample is not
  deployed, so this is recorded rather than mitigated; the extraction playbook (weeks 7 to 8) covers key
  rotation and cache lifetime for a real extraction.

## How to reverse

Reference Identity.Module from Catalog.Host and call `AddIdentityAuth`, accepting that the sample then no
longer proves Catalog builds without Identity; or delete the sample (ADR-0017).
