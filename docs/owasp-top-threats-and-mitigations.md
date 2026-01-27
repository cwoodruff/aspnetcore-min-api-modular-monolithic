### OWASP Top Threats & Mitigations for the Modular Monolith API (ASP.NET Core 10)

Last updated: 2026-01

#### Executive summary
This document is a practical, API‑focused cheat sheet of the OWASP risks most relevant to your ASP.NET Core Minimal API Modular Monolith (modules: Music, Orders, Administration, Reporting, Identity). It centers on the OWASP API Security Top 10 (2023) with concise, actionable mitigations tailored to this solution, and highlights overlaps with the general OWASP Top 10 (2021).

---

### OWASP API Security Top 10 (2023) — threats and targeted mitigations

#### API1: Broken Object Level Authorization (BOLA)
- What: Users access objects they don’t own (e.g., `GET /orders/{id}` across tenants) by guessing IDs.
- Mitigations:
  - Enforce per‑resource authorization in handlers: verify `sub` and `tenant` claims on `HttpContext.User` against the entity’s owner/tenant before returning data.
  - Avoid exposing sequential IDs externally; prefer opaque IDs/UUIDs/ULIDs.
  - Add repository/EF query filters by `tenant` and (when applicable) owner id.
  - Negative tests for cross‑tenant/object access: expect `403`.

#### API2: Broken Authentication
- What: Weak login/token issuance/validation enables account takeover.
- Mitigations:
  - Use `JwtBearer` with strict validation: `iss`, `aud`, `exp`, `nbf`, `iat`, signature, `kid`; disallow `alg=none`.
  - Asymmetric signing (RSA/ECDSA), short access token TTL (≈ 15 minutes), refresh rotation, revoke on reuse.
  - Keys in secure store (e.g., Key Vault), publish JWKS, rotate keys regularly.
  - Password hashing with PBKDF2/Argon2/bcrypt; login lockout/backoff; monitor unusual IP/device churn.

#### API3: Broken Object Property Level Authorization (Mass Assignment)
- What: Clients set or view fields they shouldn’t (`role`, `isAdmin`, `price`, etc.).
- Mitigations:
  - Use explicit request/response DTOs; never bind domain entities directly from request bodies.
  - Whitelist properties per role/policy; reject or ignore extra fields (`400` on unexpected properties where appropriate).
  - Filter sensitive properties in reads; don’t return internal cost fields to non‑privileged users.

#### API4: Unrestricted Resource Consumption
- What: Abuse of CPU/memory/DB leads to DoS.
- Mitigations:
  - Central rate limiting (burst + sustained) and concurrency limits for long‑running endpoints (e.g., Reporting exports).
  - Enforce pagination and maximum page sizes; cap upload sizes; timeouts on all I/O.
  - Cache hot/expensive reads via the central cache; consider stale‑while‑revalidate (SWR) on safe shapes.
  - Circuit breakers and retry with jitter for upstreams.

#### API5: Broken Function Level Authorization
- What: Missing/weak authz for privileged operations (admin endpoints).
- Mitigations:
  - Default‑deny: require authorization on all non‑public endpoints.
  - Use named policies like `admin.*`, `orders.write`, `music.read` via `.RequireAuthorization("policy")`.
  - For critical ops, defense‑in‑depth: validate roles/permissions again in the handler.

#### API6: Unrestricted Access to Sensitive Business Flows
- What: Automation abuses legit flows (password reset, report export).
- Mitigations:
  - Separate stricter rate limit policies for sensitive flows; consider CAPTCHA or proof‑of‑work on public flows.
  - Business throttles (per user/tenant daily export caps) and anomaly detection.

#### API7: Server‑Side Request Forgery (SSRF)
- What: API fetches attacker‑supplied URLs, reaching internal services/metadata.
- Mitigations:
  - Allowlist schemes/hosts; block link‑local/loopback/metadata IP ranges.
  - Resolve DNS and re‑check target IP; disable automatic redirects to untrusted hosts.
  - Enforce timeouts and size caps; never forward internal tokens to third‑party hosts.

#### API8: Security Misconfiguration
- What: Insecure defaults, verbose errors, weak headers, permissive CORS.
- Mitigations:
  - Enforce HTTPS at edge; HSTS in production; apply secure headers per `docs/secure-headers-plan.md`.
  - Strict CORS allowlist; never use `*` with credentials.
  - Disable detailed errors in production; return `ProblemDetails` without stack traces.
  - Keep frameworks/NuGet patched; lock down TLS ciphers/protocols.

#### API9: Improper Inventory Management
- What: Shadow/old versions of APIs are exposed without auth/monitoring.
- Mitigations:
  - Maintain an endpoint inventory and versioning policy; deprecate and remove old routes.
  - Centralized OpenAPI; restrict or protect Swagger in production.

#### API10: Unsafe Consumption of APIs
- What: Trusting upstream APIs or unsafe deserialization.
- Mitigations:
  - Validate and sanitize inbound/outbound data; enforce schemas.
  - Timeouts, retries, circuit breakers; never deserialize untrusted data into domain types directly.
  - Do not propagate upstream PII/secrets to clients; log minimally.

---

### High‑impact items from OWASP Top 10 (2021) — relevant overlaps
- Injection (`A03`): Parameterized SQL/EF Core; validate inputs; never build SQL by string concatenation.
- Cryptographic Failures (`A02`): TLS 1.2+; strong ciphers; keys in Key Vault; modern password hashing; no custom crypto.
- Insecure Design (`A04`): Least privilege by module; threat model critical flows; plan for abuse cases.
- Security Logging & Monitoring (`A09`): Log auth failures and 429s; alert on spikes; secure log retention.
- SSRF (`A10`): Covered above; enforce egress controls and URL allowlists.

---

### Identity/JWT safeguards (Identity module)
- Validate: signature, `iss`, `aud`, `exp`, `nbf`, `iat`, `kid`. Disallow `alg=none`.
- Signing: prefer RSA‑2048 or ECDSA P‑256; publish JWKS; rotate keys; pin `kid`.
- Token lifetimes: short access tokens (~15 min), refresh rotation (7–30 days) with reuse detection and revocation.
- Claims hygiene: minimum required claims; avoid PII; include `jti` for replay protection.
- Storage: keep signing keys in Key Vault/HSM; never in source control.

---

### Caching & data safety
- Never cache secrets, access tokens, refresh tokens, or authorization decisions.
- If caching sensitive‑but‑cacheable fragments (e.g., user preferences), use strict TTLs and per‑tenant keys; encrypt at rest for distributed caches.
- Namespaced keys with module/entity/version/tenant; bulk‑invalidate on role/user/tenant changes.
- Protect against cache poisoning: validate inputs before computing/storing cache values; segregate anonymous vs. authenticated variants.

---

### Secure headers & transport
- HTTPS everywhere; HSTS in production; redirect HTTP→HTTPS at edge (see `docs/https-enforcement-plan.md`).
- CORS: strict allowlist; allow only required methods/headers; avoid credentials unless using BFF.
- CSP: default `default-src 'none'` for API JSON; Swagger UI gets a tailored CSP (hashes or nonces) rolled out in Report‑Only first (see `docs/secure-headers-plan.md`).
- Additional headers: `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` deny‑by‑default; COOP/COEP/CORP as applicable.

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
- See [validation-strategy.md](validation-strategy.md) for complete implementation details

---

### Database and EF Core hardening
- Always parameterized queries; no raw SQL concatenation.
- Apply global query filters for `tenant` where applicable; verify object ownership on reads.
- Use a least‑privilege DB user; restrict schema changes to migration processes.

---

### File handling & uploads (if/when introduced)
- Limit file size and type; verify content types and magic bytes; scan for malware.
- Store outside web root; randomize filenames; strip metadata.
- Avoid vulnerable image/file processing libraries; sandbox where possible.

---

### Observability & incident response
- Metrics: auth failures, permission denials (`403`), rate limit `429`s, cache hit/miss, key rotation events, validation errors.
- Logs: structured with correlation IDs; never log secrets or raw tokens (log `jti` or a token fingerprint only).
- Alerts: spikes in `401/403/429/5xx`, unusual tenant/user activity, key/JWKS failures.
- Runbooks: token/key rotation, cache purge, rate limiter kill switch, CORS/CSP misconfig remediation.

---

### Quick, prioritized checklist
1) Identity/JWT
- Strict `JwtBearer` validation; asymmetric signing; JWKS; short TTLs; refresh rotation.
2) Authorization
- Default‑deny; named policies; per‑entity BOLA checks; tenant scoping.
3) Inputs & outputs
- DTO whitelist; validate inputs; cap payloads; paginate; hide sensitive fields.
4) Abuse safeguards
- Central rate limiting; concurrency caps for heavy endpoints; safe output caching.
5) Transport & headers
- Enforce HTTPS at edge; HSTS; strict CORS; CSP for Swagger; baseline security headers.
6) Caching discipline
- No tokens/PII/authorization decisions in cache; namespaced keys; invalidate on role/user changes.
7) Secrets & crypto
- Keys in Key Vault; regular rotation; strong TLS; modern password hashing.
8) Hardening & ops
- No verbose errors in prod; patch dependencies; structured logs/metrics/alerts; tested runbooks.

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
