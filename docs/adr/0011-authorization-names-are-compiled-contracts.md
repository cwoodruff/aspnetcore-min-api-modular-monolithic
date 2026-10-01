# 0011. Authorization names are compiled constants in Identity.Contracts

- Status: Accepted
- Date: 2026-10-01
- Phase: 4 (see `docs/upgrade-plan.md`)

## Context

Identity registers the authorization policies; every other module applies
them. Until phase 4 the names were strings: Identity kept them in an
internal `Permissions` class and private constants, and Catalog and Orders
wrote them as 25 `RequireAuthorization("catalog.read")`-style literals
(Administration had its own private copies). A rename on one side compiled
fine and failed at runtime as a 403 on the other. This happened once during
the Music-to-Catalog rename and only the integration tests caught it.

## Decision

- `Identity.Contracts/Permissions.cs` and `Policies.cs` hold every name as a
  public `const`: one constant per permission (each permission is also the
  name of the policy that requires it), plus `Policies.Admin` and
  `Policies.TenantScoped`.
- Identity registers its policies from these constants. Every other module
  references `Identity.Contracts` and applies policies only through them.
  There is no `RequireAuthorization("...")` literal anywhere in `src/`.
- Enforcement:
  - `GuardrailTests.Every_Endpoint_Policy_Name_Is_An_Identity_Contracts_Constant`
    reads every endpoint's `IAuthorizeData` at runtime and fails on a policy
    name that is not one of those constants (a literal that happens to be
    spelled right still passes, but cannot drift once the constant changes).
  - `GuardrailTests.Every_Authorization_Policy_Referenced_By_An_Endpoint_Is_Registered`
    and the startup check (`ModuleComposition.ValidateEndpoints`) still fail
    on a name Identity does not register.

## Consistency

Not a data boundary. Modules depend on Identity's names at compile time
through `Identity.Contracts`, never on `Identity.Module`.

## Consequences

- Renaming a permission is one change in `Identity.Contracts`; every use
  follows or fails to compile.
- `Identity.Contracts` becomes a dependency of every module that protects
  an endpoint. It holds constants only (`FenceTests` enforces the
  Contracts shape), so the dependency carries no behaviour.
- The claim values in issued tokens are the same strings, so existing tokens
  keep working.

## How to reverse

Inline the constant values back as strings at each call site and delete the
two files. Nothing else depends on them.
