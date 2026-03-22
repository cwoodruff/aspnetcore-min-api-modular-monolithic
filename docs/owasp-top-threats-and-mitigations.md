### OWASP Top Threats & Mitigations for the Modular Monolith API (ASP.NET Core 10)

Last updated: 2026-03

#### Executive summary

This document is a practical, API‑focused cheat sheet of the OWASP risks most
relevant to your ASP.NET Core Minimal API Modular Monolith (modules: Music,
Orders, Administration, Reporting, Identity). It centers on the OWASP API
Security Top 10 (2023) with concise, actionable mitigations tailored to this
solution, and highlights overlaps with the general OWASP Top 10 (2021).

---

### OWASP API Security Top 10 (2023) — threats and targeted mitigations

#### API1: Broken Object Level Authorization (BOLA)

- What: Users access objects they don’t own (e.g., `GET /orders/{id}` across
  tenants) by guessing IDs.
- Mitigations:
    - Enforce per‑resource authorization in handlers: verify `sub` and `tenant`
      claims on `HttpContext.User` against the entity’s owner/tenant before
      returning data.
    - Avoid exposing sequential IDs externally; prefer opaque IDs/UUIDs/ULIDs.
    - Add repository/EF query filters by `tenant` and (when applicable) owner
      id.
    - Negative tests for cross‑tenant/object access: expect `403`.

#### API2: Broken Authentication

- What: Weak login/token issuance/validation enables account takeover.
- Mitigations:
    - Use `JwtBearer` with strict validation: `iss`, `aud`, `exp`, `nbf`, `iat`,
      signature, `kid`; disallow `alg=none`.
    - Asymmetric signing (RSA/ECDSA), short access token TTL (≈ 15 minutes),
      refresh rotation, revoke on reuse.
    - Keys in secure store (e.g., Key Vault), publish JWKS, rotate keys
      regularly.
    - Password hashing with PBKDF2/Argon2/bcrypt; login lockout/backoff; monitor
      unusual IP/device churn.

#### API3: Broken Object Property Level Authorization (Mass Assignment)

- What: Clients set or view fields they shouldn’t (`role`, `isAdmin`, `price`,
  etc.).
- Mitigations:
    - Use explicit request/response DTOs; never bind domain entities directly
      from request bodies.
    - Whitelist properties per role/policy; reject or ignore extra fields (`400`
      on unexpected properties where appropriate).
    - Filter sensitive properties in reads; don’t return internal cost fields to
      non‑privileged users.

#### API4: Unrestricted Resource Consumption

- What: Abuse of CPU/memory/DB leads to DoS.
- Mitigations:
    - Central rate limiting (burst + sustained) and concurrency limits for
      long‑running endpoints (e.g., Reporting exports).
    - Enforce pagination and maximum page sizes; cap upload sizes; timeouts on
      all I/O.
    - Cache hot/expensive reads via the central cache; consider
      stale‑while‑revalidate (SWR) on safe shapes.
    - Circuit breakers and retry with jitter for upstreams.

#### API5: Broken Function Level Authorization

- What: Missing/weak authz for privileged operations (admin endpoints).
- Mitigations:
    - Default‑deny: require authorization on all non‑public endpoints.
    - Use named policies like `admin.*`, `orders.write`, `music.read` via
      `.RequireAuthorization("policy")`.
    - For critical ops, defense‑in‑depth: validate roles/permissions again in
      the handler.

#### API6: Unrestricted Access to Sensitive Business Flows

- What: Automation abuses legit flows (password reset, report export).
- Mitigations:
    - Separate stricter rate limit policies for sensitive flows; consider
      CAPTCHA or proof‑of‑work on public flows.
    - Business throttles (per user/tenant daily export caps) and anomaly
      detection.

#### API7: Server‑Side Request Forgery (SSRF)

- What: API fetches attacker‑supplied URLs, reaching internal services/metadata.
- Mitigations:
    - Allowlist schemes/hosts; block link‑local/loopback/metadata IP ranges.
    - Resolve DNS and re‑check target IP; disable automatic redirects to
      untrusted hosts.
    - Enforce timeouts and size caps; never forward internal tokens to
      third‑party hosts.

#### API8: Security Misconfiguration

- What: Insecure defaults, verbose errors, weak headers, permissive CORS.
- Mitigations:
    - Enforce HTTPS at edge; HSTS in production; apply secure headers per
      `docs/secure-headers-plan.md`.
    - Strict CORS allowlist; never use `*` with credentials.
    - Disable detailed errors in production; return `ProblemDetails` without
      stack traces.
    - Keep frameworks/NuGet patched; lock down TLS ciphers/protocols.

#### API9: Improper Inventory Management

- What: Shadow/old versions of APIs are exposed without auth/monitoring.
- Mitigations:
    - Maintain an endpoint inventory and versioning policy; deprecate and remove
      old routes.
    - Centralized OpenAPI; restrict or protect Swagger in production.

#### API10: Unsafe Consumption of APIs

- What: Trusting upstream APIs or unsafe deserialization.
- Mitigations:
    - Validate and sanitize inbound/outbound data; enforce schemas.
    - Timeouts, retries, circuit breakers; never deserialize untrusted data into
      domain types directly.
    - Do not propagate upstream PII/secrets to clients; log minimally.

---

### OWASP Top 10 Application Security Risks (2021) — full coverage

#### A01:2021 — Broken Access Control

- What: Users act outside their intended permissions — viewing other tenants'
  data, escalating to admin, or bypassing authorization on endpoints.
- Relevance: This solution uses policy-based authorization (`music.read`,
  `orders.write`, `tenant.scoped`, `role.admin`) and tenant scoping via
  `X-Tenant-Id` header with `TenantAuthorizationHandler`.
- Mitigations:
    - Default-deny: all non-public endpoints require `.RequireAuthorization()`.
    - Named permission policies enforce least-privilege per module and operation.
    - Tenant isolation via `TenantAuthorizationHandler` validates `X-Tenant-Id`
      against user claims on every tenant-scoped request.
    - Repository-level query filters should enforce tenant boundaries so that
      even a logic bug in a handler cannot leak cross-tenant data.
    - Negative integration tests assert `401` (no token), `403` (wrong
      permission/tenant), and correct `200` for valid requests.
    - Avoid exposing sequential integer IDs externally where possible; prefer
      opaque identifiers (UUIDs/ULIDs) for new entities.

#### A02:2021 — Cryptographic Failures

- What: Weak or missing encryption exposes sensitive data in transit or at rest.
- Relevance: JWT signing keys, password hashing, TLS configuration, and SQLite
  database storage.
- Mitigations:
    - TLS 1.2+ enforced; HSTS enabled in production (see
      `docs/https-enforcement-plan.md`).
    - JWT signing uses asymmetric keys (RSA/ECDSA); keys stored in Key Vault or
      HSM in production — never in source control.
    - JWKS endpoint (`/.well-known/jwks.json`) publishes public keys; `kid`
      pinning prevents key confusion attacks.
    - Password hashing via PBKDF2/Argon2/bcrypt with sufficient iterations; no
      custom cryptographic implementations.
    - SQLite database file permissions restricted; consider SQLCipher or
      filesystem encryption for sensitive deployments.
    - No secrets (connection strings, API keys, tokens) in `appsettings.json` for
      production; use `dotnet user-secrets` locally and Key Vault in deployed
      environments.

#### A03:2021 — Injection

- What: Untrusted data sent as part of a command or query causes unintended
  execution (SQL injection, command injection, LDAP injection).
- Relevance: EF Core is the primary data access layer; FluentValidation guards
  all write endpoints.
- Mitigations:
    - EF Core generates parameterized queries by default — never concatenate user
      input into raw SQL.
    - If `FromSqlRaw` or `FromSqlInterpolated` is used, always use
      parameterized overloads; ban string concatenation in code reviews.
    - FluentValidation validates all request bodies before they reach persistence
      (length, format, regex, range constraints).
    - Route and query parameters are strongly typed (`int id`, `string name`)
      which prevents many injection vectors.
    - No shell/process execution from user input; no dynamic LINQ from untrusted
      strings.

#### A04:2021 — Insecure Design

- What: Architectural flaws that cannot be fixed by implementation alone —
  missing threat models, abuse case analysis, or defense-in-depth.
- Relevance: Modular monolith architecture provides natural boundaries but
  requires deliberate design choices.
- Mitigations:
    - Module isolation: each module has its own service layer, endpoints, and
      internal types (`internal` by default); only `IModule` is public.
    - Layered defense: authorization checked at endpoint level AND can be
      re-verified in service/handler for critical operations.
    - Threat model critical flows: login, token refresh, order creation, admin
      operations — document expected abuse cases and rate-limit accordingly.
    - Separation of read and write policies: `music.read` vs `music.write`,
      `orders.read` vs `orders.write` — prevents read-only users from mutating.
    - Design for tenant isolation from day one: `TenantAuthorizationHandler`,
      tenant-scoped cache keys, and (planned) EF global query filters.

#### A05:2021 — Security Misconfiguration

- What: Insecure defaults, incomplete configurations, verbose error messages,
  unnecessary features enabled, or permissive CORS.
- Relevance: Maps to API8 above. The solution uses `ProblemDetails`, CORS
  allowlists, and environment-aware middleware.
- Mitigations:
    - `ProblemDetails` middleware returns structured errors without stack traces
      in production.
    - CORS policy "Default" allowlists only specific localhost origins (ports
      3000, 4200, 5173); never uses `*` with credentials.
    - Swagger UI should be disabled or auth-protected in production deployments.
    - `TreatWarningsAsErrors` is enabled in build; StyleCop analyzers available
      via `EnableStyleCop=true`.
    - Security headers applied per `docs/secure-headers-plan.md`:
      `X-Content-Type-Options: nosniff`, `Referrer-Policy`,
      `Permissions-Policy`, CSP.
    - Keep .NET SDK, NuGet packages, and runtime patched; use `dotnet outdated`
      or Dependabot to track vulnerable dependencies.
    - Remove or restrict debug/diagnostic endpoints before production deployment.

#### A06:2021 — Vulnerable and Outdated Components

- What: Using components with known vulnerabilities, or failing to track and
  update dependencies.
- Relevance: The solution depends on numerous NuGet packages (EF Core,
  FluentValidation, JWT libraries, etc.) and the .NET runtime.
- Mitigations:
    - Use Central Package Management (`Directory.Packages.props`) to pin and
      manage versions consistently across all projects.
    - Run `dotnet list package --vulnerable` regularly to detect known CVEs.
    - Enable Dependabot or GitHub security alerts on the repository.
    - Subscribe to .NET security advisories and ASP.NET Core patch announcements.
    - Audit transitive dependencies — a direct package may pull in a vulnerable
      transitive dependency.
    - Pin exact package versions in library projects; update deliberately with
      testing, not automatically in production builds.
    - Remove unused NuGet packages to reduce attack surface.

#### A07:2021 — Identification and Authentication Failures

- What: Weak authentication mechanisms allow brute force, credential stuffing,
  session hijacking, or token theft.
- Relevance: Maps to API2 above. The Identity module handles login, token
  issuance, refresh, and logout.
- Mitigations:
    - JWT Bearer validation enforces `iss`, `aud`, `exp`, `nbf`, `iat`,
      signature, and `kid`; `alg=none` is disallowed.
    - Short access token TTL (~15 minutes); refresh token rotation with reuse
      detection and revocation on logout.
    - Login lockout/backoff after repeated failures; monitor for credential
      stuffing patterns (unusual IP/user-agent churn).
    - Refresh tokens are single-use; reuse triggers revocation of the entire
      token family.
    - Password requirements enforced; hashing with modern algorithms (PBKDF2 with
      high iteration count minimum, or Argon2/bcrypt).
    - `jti` claim in tokens enables replay detection and revocation lists.

#### A08:2021 — Software and Data Integrity Failures

- What: Code and infrastructure that does not protect against integrity
  violations — insecure CI/CD pipelines, unsigned packages, or untrusted
  deserialization.
- Relevance: NuGet package supply chain, CI/CD pipeline security, and JSON
  deserialization in API endpoints.
- Mitigations:
    - Verify NuGet package signatures; use `nuget.org` as the sole trusted
      package source; avoid unvetted third-party feeds.
    - CI/CD pipelines should use locked/pinned dependency versions
      (`dotnet restore --locked-mode` with `packages.lock.json`).
    - Protect CI/CD secrets (deploy keys, service principals) — never expose in
      logs or artifacts.
    - JSON deserialization uses `System.Text.Json` with strict options;
      `PropertyNamingPolicy` is null (PascalCase); unknown properties are
      ignored by default but never bound to privileged fields.
    - Never deserialize untrusted data into domain entities directly — always use
      explicit DTOs (request/response models in `SharedKernel.Persistence/ApiModels/`).
    - Sign release artifacts and Docker images in production pipelines.

#### A09:2021 — Security Logging and Monitoring Failures

- What: Insufficient logging, monitoring, or alerting allows attacks to go
  undetected and unresponded.
- Relevance: The solution uses built-in ASP.NET Core logging; no Serilog or
  OpenTelemetry is configured out-of-the-box.
- Mitigations:
    - Log all authentication failures (`401`), authorization denials (`403`),
      rate limit rejections (`429`), and validation errors (`400`).
    - Use structured logging with `ILogger<T>` and correlation IDs for request
      tracing across modules.
    - Never log secrets, raw tokens, passwords, or PII — log `jti` or a token
      fingerprint only.
    - Alert on spikes in `401/403/429/5xx` responses and unusual tenant/user
      activity patterns.
    - Retain logs securely with tamper-evident storage; restrict log access to
      authorized personnel.
    - Plan for Application Insights, OpenTelemetry, or equivalent for production
      deployments — the logging abstraction (`ILogger<T>`) makes this a
      configuration change, not a code change.
    - Document runbooks for: token/key rotation, cache purge, rate limiter kill
      switch, CORS/CSP misconfiguration remediation.

#### A10:2021 — Server-Side Request Forgery (SSRF)

- What: The application fetches attacker-supplied URLs, allowing access to
  internal services, cloud metadata endpoints, or private networks.
- Relevance: Maps to API7 above. Currently the API does not fetch external URLs
  based on user input, but this must be guarded if such features are added.
- Mitigations:
    - Allowlist permitted schemes (`https` only) and hosts; block
      link-local/loopback/metadata IP ranges (`169.254.x.x`, `127.0.0.1`,
      `[::1]`, cloud metadata IPs).
    - Resolve DNS and re-check the target IP before making the request to prevent
      DNS rebinding attacks.
    - Disable automatic HTTP redirects to untrusted hosts.
    - Enforce timeouts and response size caps on all outbound HTTP calls.
    - Never forward internal tokens or credentials to third-party hosts.
    - Use `IHttpClientFactory` with named/typed clients that have pre-configured
      base addresses and timeouts.

---

### Identity/JWT safeguards (Identity module)

- Validate: signature, `iss`, `aud`, `exp`, `nbf`, `iat`, `kid`. Disallow
  `alg=none`.
- Signing: prefer RSA‑2048 or ECDSA P‑256; publish JWKS; rotate keys; pin `kid`.
- Token lifetimes: short access tokens (~15 min), refresh rotation (7–30 days)
  with reuse detection and revocation.
- Claims hygiene: minimum required claims; avoid PII; include `jti` for replay
  protection.
- Storage: keep signing keys in Key Vault/HSM; never in source control.

---

### Caching & data safety

- Never cache secrets, access tokens, refresh tokens, or authorization
  decisions.
- If caching sensitive‑but‑cacheable fragments (e.g., user preferences), use
  strict TTLs and per‑tenant keys; encrypt at rest for distributed caches.
- Namespaced keys with module/entity/version/tenant; bulk‑invalidate on
  role/user/tenant changes.
- Protect against cache poisoning: validate inputs before computing/storing
  cache values; segregate anonymous vs. authenticated variants.

---

### Secure headers & transport

- HTTPS everywhere; HSTS in production; redirect HTTP→HTTPS at edge (see
  `docs/https-enforcement-plan.md`).
- CORS: strict allowlist; allow only required methods/headers; avoid credentials
  unless using BFF.
- CSP: default `default-src 'none'` for API JSON; Swagger UI gets a tailored
  CSP (hashes or nonces) rolled out in Report‑Only first (see
  `docs/secure-headers-plan.md`).
- Additional headers: `X-Content-Type-Options: nosniff`,
  `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`
  deny‑by‑default; COOP/COEP/CORP as applicable.

---

### Input validation & output handling

- Validate route/query/body inputs for length, format, range; reject on failure.
- Cap JSON payload size; enforce pagination and maximum page size.
- Avoid regex DoS; use efficient validators.
- Output minimal necessary fields; redact or omit sensitive data.

### Service layer validation (implemented)

- All write operations use FluentValidation before persistence
- Validators are centralized in `SharedKernel.Persistence/Validation/`
- Validation rules include:
    - Required field checks (`NotNull()`, `NotEmpty()`)
    - Length constraints (`MaximumLength()`)
    - Format validation (`EmailAddress()`, `Matches()` for regex)
    - Business rules (`GreaterThan()`, `LessThanOrEqualTo()`)
- ValidationException thrown on failure, converted to HTTP 400 ProblemDetails
- Example validators: CustomerValidator, TrackValidator, InvoiceValidator
- See [validation-strategy.md](validation-strategy.md) for complete
  implementation details

---

### Database and EF Core hardening

- Always parameterized queries; no raw SQL concatenation.
- Apply global query filters for `tenant` where applicable; verify object
  ownership on reads.
- Use a least‑privilege DB user; restrict schema changes to migration processes.

---

### File handling & uploads (if/when introduced)

- Limit file size and type; verify content types and magic bytes; scan for
  malware.
- Store outside web root; randomize filenames; strip metadata.
- Avoid vulnerable image/file processing libraries; sandbox where possible.

---

### Observability & incident response

- Metrics: auth failures, permission denials (`403`), rate limit `429`s, cache
  hit/miss, key rotation events, validation errors.
- Logs: structured with correlation IDs; never log secrets or raw tokens (log
  `jti` or a token fingerprint only).
- Alerts: spikes in `401/403/429/5xx`, unusual tenant/user activity, key/JWKS
  failures.
- Runbooks: token/key rotation, cache purge, rate limiter kill switch, CORS/CSP
  misconfig remediation.

---

### Cross-reference: OWASP Top 10 (2021) ↔ API Security Top 10 (2023)

| OWASP Top 10 (2021)                  | API Security Top 10 (2023) | Status          |
|---------------------------------------|----------------------------|-----------------|
| A01 Broken Access Control             | API1, API5                 | Implemented     |
| A02 Cryptographic Failures            | —                          | Partially impl. |
| A03 Injection                         | —                          | Implemented     |
| A04 Insecure Design                   | —                          | Implemented     |
| A05 Security Misconfiguration         | API8                       | Partially impl. |
| A06 Vulnerable & Outdated Components  | —                          | Process needed  |
| A07 Identification & Auth Failures    | API2                       | Implemented     |
| A08 Software & Data Integrity         | API10                      | Process needed  |
| A09 Security Logging & Monitoring     | —                          | Planned         |
| A10 SSRF                              | API7                       | N/A (no feature)|

### Quick, prioritized checklist

1) Identity/JWT (A07, API2)

- Strict `JwtBearer` validation; asymmetric signing; JWKS; short TTLs; refresh
  rotation.

2) Authorization (A01, API1, API5)

- Default‑deny; named policies; per‑entity BOLA checks; tenant scoping.

3) Inputs & outputs (A03, API3)

- DTO whitelist; validate inputs; cap payloads; paginate; hide sensitive fields.

4) Abuse safeguards (API4, API6)

- Central rate limiting; concurrency caps for heavy endpoints; safe output
  caching.

5) Transport & headers (A02, A05, API8)

- Enforce HTTPS at edge; HSTS; strict CORS; CSP for Swagger; baseline security
  headers.

6) Caching discipline

- No tokens/PII/authorization decisions in cache; namespaced keys; invalidate on
  role/user changes.

7) Secrets & crypto (A02)

- Keys in Key Vault; regular rotation; strong TLS; modern password hashing.

8) Supply chain & integrity (A06, A08)

- Verify NuGet signatures; pin dependency versions; lock files in CI; sign
  release artifacts; audit transitive dependencies.

9) Logging & monitoring (A09)

- Structured logs with correlation IDs; alert on auth/rate-limit spikes; no
  secrets in logs; runbooks for incident response.

10) Hardening & ops (A05, API8, API9)

- No verbose errors in prod; patch dependencies; disable Swagger in production;
  maintain endpoint inventory; tested runbooks.

---

### References & related docs

- OWASP API Security Top 10 (2023): https://owasp.org/API-Security/
- OWASP Top 10 (2021): https://owasp.org/www-project-top-ten/
- Secure headers plan: `docs/secure-headers-plan.md`
- HTTPS enforcement plan: `docs/https-enforcement-plan.md`
- Rate limiting plan: `docs/rate-limiting-plan.md`
- Caching strategy: `docs/caching-strategy.md`

---

### Non‑goals (confirmed)

- Changing any existing code here.
- Choosing a vendor‑locked approach without portable fallback.
- Caching mutable security artifacts (access tokens, refresh tokens).
