# Secure Headers Plan for the Modular Monolith API (ASP.NET Core 9/10, Minimal APIs)

This document defines a configuration-first, incrementally adoptable plan for applying modern HTTP security headers to the API host while keeping modules isolated from transport/security concerns. No code changes are introduced by this document; it is an implementation guide and blueprint for a future PR.

Non‑goals
- Refactor existing modules or add identity logic outside the Identity module
- Enforce headers today without an observe period
- Provide CDN/proxy-specific configs (notes provided; exact syntax deferred)

---

## 1) Objectives and guiding principles
- Default‑deny posture: minimize browser attack surface for a JSON API; Swagger is the only HTML UI and gets a tailored policy.
- Environment‑aware: strict in Production; pragmatic in Development (CSP in Report‑Only, allow localhost origins).
- Centralized at the host: apply headers in the API host middleware; modules remain unaware of headers.
- Observable and reversible: start with report‑only, measure, then enforce; all header behaviors are toggleable via configuration.

---

## 2) Baseline headers to adopt globally

Apply to all API responses unless overridden by a path‑specific policy.

- HTTPS & HSTS
  - Enforce HTTPS redirection (already on) and enable HSTS in non‑Development.
  - Production HSTS: `max-age=31536000` (1 year), `includeSubDomains=true` (only if all subdomains are HTTPS), `preload=true` after readiness.
- X‑Content‑Type‑Options: `nosniff`
  - Prevents MIME sniffing on JSON/static assets.
- Referrer‑Policy: `strict-origin-when-cross-origin` (or `no-referrer` if analytics do not need referrers).
- Frame protections
  - Prefer CSP `frame-ancestors 'none'` globally (blocks clickjacking). Optionally add legacy `X-Frame-Options: DENY` for compatibility.
- Cross‑origin isolation family (introduce carefully; test with Swagger)
  - `Cross-Origin-Opener-Policy: same-origin`
  - `Cross-Origin-Embedder-Policy: require-corp` (evaluate impact before enforcing)
  - `Cross-Origin-Resource-Policy: same-site` (or `same-origin`) for API JSON
- Permissions‑Policy (deny‑by‑default)
  - e.g., `geolocation=()`, `camera=()`, `microphone=()`, `fullscreen=()`, etc.; enable selectively if needed.

---

## 3) CORS strategy (central policy, per‑environment via configuration)

- Development
  - Allow explicit localhost origins (http/https ports: 3000, 4200, 5173).
  - Allow methods: `GET, POST, PUT, DELETE, OPTIONS` (narrow when possible).
  - Allow headers: `Authorization, Content-Type`; avoid `AllowAnyHeader` where possible.
  - Do not allow credentials unless we adopt a BFF/cookie flow.
- Staging/Production
  - Strict allowlist from configuration (exact origins, no wildcards). Consider separate named policies per frontend.
  - Preflight caching: `Access-Control-Max-Age` 600–1800 seconds to reduce OPTIONS load.
  - Expose only required headers; never expose sensitive headers.
  - Enable credentials only if strictly required.
- CDN/reverse proxy
  - Ensure CORS headers are set in exactly one layer (CDN vs API) to avoid conflicts.

---

## 4) CSP strategy tailored to an API host

- API default CSP (most routes return JSON)
  - `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'`.
  - Effectively says: API responses are not executable content and cannot be embedded.
- Swagger path override (`/swagger`, `/swagger/index.html`, `/swagger/**`)
  - Allow only what Swagger requires. Recommended initial directives:
    - `default-src 'none'`
    - `script-src 'self' 'sha256-…'` (hashes) or `nonce-…`
    - `style-src 'self' 'sha256-…'` (Dev may start with `'unsafe-inline'`)
    - `img-src 'self' data:`
    - `connect-src 'self'`
    - `font-src 'self'`
    - `frame-ancestors 'none'`
  - Rollout: start with `Content-Security-Policy-Report-Only` to capture violations, then enforce once tuned.
- Reporting endpoint (optional)
  - Configure CSP `report-to`/`report-uri` to collect violations; throttle logs to prevent noise.

---

## 5) Authentication endpoints and caching

- CORS: Token endpoints (`/api/identity/login`, `/refresh`, `/logout`) use the same strict allowlist as the API; avoid wildcard origins.
- Cache‑Control for auth responses: `Cache-Control: no-store`, `Pragma: no-cache`, `Expires: 0` to prevent intermediaries from caching token responses.
- Swagger and auth: Swagger can remain public in non‑prod; in Production consider protecting Swagger (RequireAuthorization or IP allowlists).

---

## 6) Additional cache/security headers on API responses

- JSON APIs: choose Cache‑Control per sensitivity.
  - Typical dynamic JSON: `Cache-Control: no-store` or `private, max-age=0`.
  - Health endpoints: optional short `max-age` (30–60s) if desired.
- Validators: Prefer `ETag/If-None-Match` and `Last-Modified/If-Modified-Since` for client caching of idempotent GETs.
- Server header hygiene: remove `Server` and `X-Powered-By` at the reverse proxy; avoid leaking framework versions.

---

## 7) Configuration model (no code changes now; example keys)

- `SecurityHeaders:Enabled` = true|false
- `SecurityHeaders:Hsts:MaxAgeSeconds`, `IncludeSubDomains`, `Preload`
- `SecurityHeaders:ReferrerPolicy` = `strict-origin-when-cross-origin` | `no-referrer`
- `SecurityHeaders:PermissionsPolicy` (key/value map, e.g., `{ geolocation: "()", camera: "()" }`)
- `SecurityHeaders:Cors:AllowedOrigins` = ["https://app.example.com", "https://admin.example.com"]
- `SecurityHeaders:Cors:AllowedMethods` = ["GET","POST"]
- `SecurityHeaders:Cors:AllowedHeaders` = ["Authorization","Content-Type"]
- `SecurityHeaders:Cors:AllowCredentials` = false
- `SecurityHeaders:Csp:Mode` = Off | ReportOnly | Enforce
- `SecurityHeaders:Csp:Swagger:Enabled` = true; `SecurityHeaders:Csp:Swagger:Directives` = { … }
- `SecurityHeaders:COOP` = `same-origin`; `SecurityHeaders:COEP` = `require-corp`; `SecurityHeaders:CORP` = `same-site`

Provide `appsettings.Development.json` with Dev‑friendly defaults (CSP Report‑Only; localhost CORS), and `appsettings.Production.json` with strict allowlists and Enforce mode.

---

## 8) Path policy matrix (what headers apply where)

- `/api/*` (default): baseline headers + CSP `default-src 'none'`.
- `/api/identity/*`: baseline + CORS allowlist + `Cache-Control: no-store` + CSP `default-src 'none'`.
- `/.well-known/jwks.json`: baseline + `Cache-Control: public, max-age=300` (public key is cacheable).
- `/swagger/*`: baseline + Swagger CSP override (Report‑Only → Enforce) + optional CORS disabled (served same origin).
- Health endpoints: baseline + `Cache-Control: no-store` or short `max-age` depending on monitoring needs.

---

## 9) Rollout plan

- Phase 1 (Observe)
  - Enable CSP in Report‑Only; verify no breakage in Swagger and clients.
  - Confirm HSTS settings in non‑prod.
- Phase 2 (Enforce Baseline)
  - Enforce `nosniff`, `Referrer-Policy`, `Permissions-Policy`, `frame-ancestors`, COOP/CORP where safe.
  - Keep CSP Report‑Only if still tuning.
- Phase 3 (Enforce CSP)
  - Switch Swagger CSP to nonces/hashes; remove `'unsafe-inline'`. Enforce CSP globally.
- Phase 4 (HSTS Preload)
  - After weeks without mixed content, enable `preload` and submit domain to preload list (only if all subdomains support HTTPS).
- Continuous
  - Maintain CORS allowlists per environment; audit and prune unused origins regularly.

---

## 10) Validation and testing

- Integration tests assert presence and values of key headers for representative routes (root, `/api/music/...`, `/api/identity/...`, `/swagger/index.html`, `/.well-known/jwks.json`).
- External scans: use securityheaders.com for staging/prod baselines.
- DAST: OWASP ZAP baseline; catch missing/weak headers.
- E2E: Playwright (or equivalent) to detect CORS/CSP regressions.
- Monitor CSP reports (if configured) and browser console errors during manual verification.

---

## 11) Ops & risk management

- Dashboards: header coverage %, CSP violation rate, preflight failure counts, CORS violations.
- Alerts: spikes in CSP violations (> X/min), preflight 4xx/5xx rates, missing header checks.
- Risks and mitigations
  - CORS misconfiguration → temporary breaker to disable CORS; runbook for origin updates.
  - CSP breakage (Swagger/assets) → keep Report‑Only until violations are zero; path‑specific CSP; use nonces/hashes.
  - COEP/COOP disruptions → roll out after testing embeddings; keep toggles to disable.
  - HSTS preload lock‑in → enable only after org‑wide HTTPS is guaranteed.

---

## 12) Future implementation location

- Host-level middleware adds these headers before endpoint mapping; Identity remains the sole owner of AuthN/AuthZ.
- Configuration from appsettings and environment variables controls behavior.
- Modules remain unchanged; they only declare auth policies as needed.

---

## 13) Sample configuration (illustrative only)

appsettings.Development.json
```json
{
  "SecurityHeaders": {
    "Enabled": true,
    "Hsts": { "MaxAgeSeconds": 0, "IncludeSubDomains": false, "Preload": false },
    "ReferrerPolicy": "strict-origin-when-cross-origin",
    "PermissionsPolicy": { "geolocation": "()", "camera": "()", "microphone": "()", "fullscreen": "()" },
    "Cors": {
      "AllowedOrigins": ["http://localhost:3000", "http://localhost:4200", "http://localhost:5173", "https://localhost:3000", "https://localhost:4200", "https://localhost:5173"],
      "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      "AllowedHeaders": ["Authorization", "Content-Type"],
      "AllowCredentials": false
    },
    "Csp": { "Mode": "ReportOnly" },
    "COOP": "same-origin", "COEP": "require-corp", "CORP": "same-site"
  }
}
```

appsettings.Production.json
```json
{
  "SecurityHeaders": {
    "Enabled": true,
    "Hsts": { "MaxAgeSeconds": 31536000, "IncludeSubDomains": true, "Preload": true },
    "ReferrerPolicy": "no-referrer",
    "PermissionsPolicy": { "geolocation": "()", "camera": "()", "microphone": "()", "fullscreen": "()" },
    "Cors": {
      "AllowedOrigins": ["https://app.example.com", "https://admin.example.com"],
      "AllowedMethods": ["GET", "POST"],
      "AllowedHeaders": ["Authorization", "Content-Type"],
      "AllowCredentials": false,
      "MaxAgeSeconds": 1800
    },
    "Csp": {
      "Mode": "Enforce",
      "Swagger": {
        "Enabled": true,
        "Directives": {
          "default-src": "'none'",
          "script-src": "'self' 'sha256-…'",
          "style-src": "'self' 'sha256-…'",
          "img-src": "'self' data:",
          "connect-src": "'self'",
          "font-src": "'self'",
          "frame-ancestors": "'none'"
        }
      }
    },
    "COOP": "same-origin", "COEP": "require-corp", "CORP": "same-site"
  }
}
```

---

## 14) ADR (Appendix)

- Title: Adopt centralized secure headers with environment‑aware policies
- Status: Proposed
- Context: Multi‑instance Minimal API host serving JSON with Swagger UI; need strong browser‑facing security defaults without disrupting developer workflows.
- Decision: Centralize secure headers (HSTS, CORS, CSP, Permissions‑Policy, COOP/COEP/CORP) at the host with configuration switches and path‑specific overrides; start with CSP in Report‑Only and tighten iteratively.
- Consequences: Reduced attack surface for browser‑initiated requests; increased operational diligence (CORS origin management, CSP tuning). Swagger may require nonce/hash maintenance in Enforce mode.
