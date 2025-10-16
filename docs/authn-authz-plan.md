### Executive Summary
We will introduce JWT bearer authentication for the Modular Monolith, centralizing all authentication and authorization logic inside the Identity module. The Identity module will issue and validate tokens, define policies, manage refresh tokens, and expose JWKS for key distribution. Other modules (Music, Orders, Administration, Reporting) will remain identity-agnostic, simply marking endpoints with RequireAuthorization() or RequireAuthorization("policy-name") as needed.

The solution uses RS256 (RSA) signing with a pluggable key management abstraction. A dev-friendly in-memory key provider is scaffolded now, while production guidance recommends a managed KMS (e.g., Azure Key Vault) and periodic key rotation with a short overlap window. Access tokens are short-lived (15 minutes), refresh tokens are longer (7 days, sliding rotation optional). Claims include permissions and roles, enabling policy-based authorization across modules.

### Architecture Overview
- Flow: Front-end -> API Host -> Authentication middleware (JwtBearer) -> Authorization policy evaluation -> Module endpoint handler.
- Identity module responsibilities:
  - User validation (scaffolded in-memory demo user store only).
  - Token issuance (access + refresh), refresh rotation, logout/revocation.
  - JWKS endpoint for public key distribution and optional OIDC discovery (scaffolded JWKS now).
  - Policy registry and permissions naming convention for all modules.
  - Pluggable key material service; dev provider included.
- Other modules:
  - Remain unaware of identity internals; they only use .RequireAuthorization() or .RequireAuthorization("policy").
  - Read claims from HttpContext.User when needed.

### Technology & Library Choices
- Options considered:
  - ASP.NET Core Identity + EF Core: full user store and flows, but heavier; can be added later.
  - OpenIddict: full OIDC provider; powerful but adds complexity; future-friendly.
  - Duende IdentityServer: commercial license for production scenarios.
  - Custom lightweight issuer (chosen for now): minimal JWT issuance/validation and policies entirely within Identity module, compatible with standard JwtBearer validation.
- Validation stack: Microsoft.AspNetCore.Authentication.JwtBearer.
- Signing algorithms: RS256 (RSA-2048) chosen for:
  - Wide validator support (browsers, gateways, other languages).
  - Private key non-shared with clients; only public key needed for validation.
- Key management:
  - Dev: in-memory RSA key per process (scaffolded DevKeyMaterialService).
  - Prod: Azure Key Vault (or equivalent) for key storage and rotation (to be plugged via IKeyMaterialService).
  - JWKS endpoint for validators to fetch public keys; rotate keys with overlapping validation keys.

### Token Design
- Claims: iss, aud, sub, name, email, roles[], permissions[], tenant, iat, nbf, exp, jti.
- Audience: single audience (modular-api) is sufficient; can evolve to per-module audiences if needed.
- Permissions examples: music.read, music.write, orders.read, orders.write, admin.users.manage, report.view.
- TTLs: access tokens 15 minutes; refresh tokens 7 days (sliding rotation optional).
- Clock skew: 2 minutes.
- Replay protection: jti included; a store could track used JTIs for high-security flows.

### Identity Module Deliverables (scaffolded)
- Extensions/IdentityAuthExtensions.cs
  - AddIdentityAuth(IServiceCollection, IConfiguration)
  - UseIdentityAuth(IApplicationBuilder)
  - Registers JwtBearer and Authorization policies via PolicyRegistry.
- Endpoints/AuthEndpoints.cs
  - POST /api/identity/login (demo user) -> access + refresh
  - POST /api/identity/refresh -> rotate refresh
  - POST /api/identity/logout -> revoke refresh
  - GET  /api/identity/userinfo -> current claims
  - GET  /api/identity/.well-known/jwks.json -> JWKS
- Services
  - ITokenService, TokenService (RS256 issuance, kid header)
  - IRefreshTokenStore, InMemoryRefreshTokenStore
  - IUserStore, InMemoryUserStore (demo only)
- Authorization
  - Permissions.cs (string constants)
  - PolicyRegistry.cs (policies per permission; no global fallback policy)
- Key management
  - IKeyMaterialService, DevKeyMaterialService (in-memory RSA, JWKS)
- Config keys
  - Jwt: Issuer, Audience, AccessTokenMinutes, RefreshTokenDays, KeyProvider, KeyVaultVaultUri, KeyVaultKeyName

### Host Wiring (samples)
- In Program.cs before Build():
  - builder.Services.AddIdentityAuth(builder.Configuration);
- In pipeline after CORS:
  - app.UseIdentityAuth();
- OpenAPI: add a bearer security scheme and requirement (optional; not enforced in code to avoid extra package coupling from Identity module).

### Module Consumption Pattern
- Endpoints use:
  - group.MapGet("/items", ...).RequireAuthorization();
  - group.MapPost("/orders", ...).RequireAuthorization("orders.write");
- Read user claims via HttpContext.User, e.g., User.FindFirst("tenant").

### Authorization Model
- Policy-based authorization using claim-based permissions and roles.
- Naming: music.read, music.write, orders.read, orders.write, admin.users.manage, report.view.
- Role-to-permissions expansion is an Identity concern; modules remain unaware.
- Custom handlers can live in Identity module only; modules refer to policy names.

### Security Hardening
- Front-end storage:
  - Prefer BFF pattern with secure httpOnly cookies; otherwise store tokens in memory (avoid localStorage).
- CSRF: required if cookies are used; set SameSite, anti-forgery tokens.
- Claims minimization: avoid PII unless necessary; consider GDPR rights.
- Token size budgeting: keep permissions list minimal; consider scope compression.
- Key rotation: rotate RSA keys periodically; keep old key for short overlap.
- Revocation: store and revoke refresh tokens; consider device binding and IP risk checks.
- MFA: plan for TOTP/WebAuthn factors later.

### OpenAPI & Developer Experience
- Security scheme (in API host Swagger configuration):
  - Add bearer scheme and global requirement.
- Quickstart:
  - curl -X POST http://localhost:5000/api/identity/login -H "Content-Type: application/json" -d '{"username":"demo","password":"demo123!"}'
  - curl http://localhost:5000/api/music/health -H "Authorization: Bearer <token>"
- Optional: Postman collection with auth and protected endpoint examples.

### Observability & Ops
- Metrics: token issuance count, validation failures (signature/audience/expired), refresh rotations, revocations.
- Logs: structured logs with correlation IDs; never log secrets or raw tokens.
- Alerts: spikes in 401/403, repeated refresh failures, unusual IP/device churn.
- Health: JWKS reachable, key age, KMS availability (for Key Vault provider later).

### Testing Strategy
- Unit tests (Identity): token issuance, permissions mapping, policy evaluation.
- Integration (API host): login -> call protected endpoints; expired token; wrong audience; bad signature; missing permission -> 401/403.
- CI: use test keys distinct from prod; validate kid rollover.

### Rollout Plan
- Phase 1: Introduce scaffolding and host wiring (done, behind no global auth).
- Phase 2: Protect select endpoints with RequireAuthorization().
- Phase 3: Add fine-grained permission policies and gradually adopt across modules.
- Phase 4: Implement refresh rotation and revocation with persistence; add key rotation in prod via KMS.
- Phase 5: Optional OIDC discovery and external identity providers.
