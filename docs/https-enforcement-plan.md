# HTTPS Enforcement Plan (Configuration Only)

**Status: Reference guide (configuration-only, no code changes required)**

This document lists the configuration‐only steps to enforce HTTPS for the Modular Monolith API across environments. Use platform/server configuration and deployment settings to guarantee transport security.

---

## 1) Executive Summary
- Enforce HTTPS at the edge (reverse proxy, load balancer, platform) with 301 redirects from HTTP → HTTPS and HSTS enabled in production.
- Restrict listeners to HTTPS only where possible; do not publish plain HTTP externally. If HTTP must be exposed (e.g., health on an internal port), scope it to private networks only.
- Use valid TLS certificates (dev certificates locally; managed/rotated certificates in prod). Prefer terminating TLS at the edge (NGINX/Ingress/IIS/Azure Front Door/App Service).

The application already includes HSTS/HTTPS redirection middleware in non‑Development, but this plan avoids relying on app logic by enforcing HTTPS outside the app boundary.

---

## 2) Local Development
- Use the built‑in ASP.NET Core HTTPS developer certificate:
  - Run once: `dotnet dev-certs https --trust`
  - The existing launchSettings.json already exposes https://localhost:7043 in Development.
- Browser trust: Ensure the dev cert is trusted on your machine (the command above automates this).
- Optional: Configure your IDE profile to use the HTTPS URL only (keep HTTP bound for tools if needed, but do not publish it externally).

---

## 3) Azure App Service (Linux/Windows)
- Enforce HTTPS platform‑wide:
  - Set App Service setting `HTTPS Only` = On (in Azure Portal → TLS/SSL settings → HTTPS Only).
- Certificates:
  - Bind custom domain certificates via App Service Certificates or upload your own (PFX). Prefer Managed Certificates where possible.
- HSTS:
  - Enable HSTS at the edge (Front Door/Application Gateway) if used; otherwise, add an ingress/reverse proxy in front of App Service to set HSTS headers.
- Redirect behavior:
  - App Service will automatically redirect HTTP to HTTPS when `HTTPS Only` is enabled.

---

## 4) Azure Kubernetes Service (AKS) with Ingress (NGINX/AGIC)
- Terminate TLS at the Ingress Controller; do not expose NodePort/Service on HTTP from the internet.
- NGINX Ingress (examples as guidance; apply via Helm/Ingress annotations):
  - Force HTTPS redirect: `nginx.ingress.kubernetes.io/force-ssl-redirect: "true"`
  - HSTS: `nginx.ingress.kubernetes.io/hsts: "true"`, `.../hsts-max-age: "31536000"`, `.../hsts-include-subdomains: "true"`, `.../hsts-preload: "true"` (validate before preload).
- Certificates:
  - Use cert-manager with ACME/Let’s Encrypt for automated issuance/renewal; or import certificates via Kubernetes secrets.
- Internal HTTP:
  - If you keep HTTP Service (cluster‑internal) for liveness probes, restrict it to ClusterIP and NetworkPolicies; do not expose externally.

---

## 5) NGINX/Apache (VM or Container)
- Terminate TLS and redirect HTTP → HTTPS at the proxy.
- NGINX high‑level steps:
  - Server block on port 80 returns `301 https://$host$request_uri`.
  - Server block on port 443 with valid `ssl_certificate` and `ssl_certificate_key` proxies to the Kestrel HTTPS backend or to HTTP on loopback.
  - Add HSTS: `add_header Strict-Transport-Security "max-age=31536000; includeSubDomains; preload" always;` (after readiness checks).
- Apache high‑level steps:
  - Enable `mod_ssl` and `mod_rewrite`. Use `RewriteRule` on HTTP vhost to redirect to HTTPS vhost; configure TLS with your certificate chain; set `Header always set Strict-Transport-Security ...`.

---

## 6) IIS / Windows Hosting
- Bind HTTPS on the site with a valid certificate and unbind external HTTP (or redirect HTTP to HTTPS using URL Rewrite).
- Enable HSTS in IIS (site settings) or via URL Rewrite `Outbound Rules` once verified.
- When hosting behind ARR/Load Balancer, ensure only HTTPS is reachable from outside and internal hops use private networking.

---

## 7) Docker / Container Runs (Standalone)
- Provide certificates to Kestrel via environment variables and volumes (no code changes):
  - Mount PFX and set:
    - `ASPNETCORE_URLS=https://+:8443`
    - `Kestrel__Endpoints__Https__Url=https://+:8443`
    - `Kestrel__Endpoints__Https__Certificate__Path=/certs/site.pfx`
    - `Kestrel__Endpoints__Https__Certificate__Password=<pfx-password>`
  - Publish only port 8443: `-p 8443:8443` and avoid publishing an HTTP port.
- Reverse proxy pattern (recommended): Place an NGINX/Traefik container in front to terminate TLS and route to the app over an internal Docker network.

---

## 8) Certificates (Dev vs Prod)
- Development: use ASP.NET Core dev cert.
- Staging/Prod: use managed certificates (Let’s Encrypt via cert-manager, Azure App Service Managed Certs, or CA‑issued certs). Automate renewal and reload.
- Key management: protect private keys; restrict access; use Key Vault/Secrets Manager where supported.

---

## 9) HSTS Guidance
- Enable HSTS in Production at the edge with `max-age=31536000; includeSubDomains; preload` after confirming that all subdomains are HTTPS.
- Start with a smaller max‑age in staging (e.g., 300 seconds) to validate behavior. Move to 1 year when confident.
- Note: HSTS is persistent in browsers; exercise caution with `preload` until fully ready.

---

## 10) Verification Checklist
- External HTTP (80) returns 301 → HTTPS for all routes.
- No mixed content or broken redirects in Swagger.
- TLS grade A in SSL Labs for production hostname.
- HSTS header present on HTTPS responses (production).
- Health checks and probes succeed (if they use HTTP internally, they are not internet‑exposed).
- App Insights/Logs show no HTTP access from public endpoints.

---

## 11) Rollout Plan
1. Staging: enable HTTPS‑only and TLS termination at the edge; verify redirects and HSTS with low max‑age.
2. Production canary: enable HTTPS‑only for a subset of traffic or a secondary hostname; monitor.
3. Full production: enforce HTTPS‑only globally; increase HSTS max‑age to 1 year after a stable period; optionally submit to preload list.

---

## 12) FAQs
- Q: Do we need to change application code?
  A: No. This plan enforces HTTPS via platform/proxy configuration. The app already has conditional HSTS/redirect middleware for non‑Dev, but edge enforcement is preferred.

- Q: Can we keep an HTTP endpoint for health?
  A: Yes, if it’s not internet‑exposed (private network only). For public traffic, force HTTPS at the edge.

- Q: Where do we store certificates?
  A: Use platform secrets/certificate stores (Azure Key Vault + App Service bindings, cert‑manager in K8s, or Windows Certificate Store in IIS).

---

## 13) Platform‑Specific Knobs (Quick Reference)
- Azure App Service: TLS/SSL settings → HTTPS Only = On.
- Azure Front Door: Redirect rule HTTP → HTTPS; enable HSTS on the custom domain.
- Azure Application Gateway: Listener on 443 only; redirect 80 → 443; HSTS header via rewrite rules.
- AKS NGINX Ingress: `force-ssl-redirect: "true"`, HSTS annotations as above.
- IIS: Bind HTTPS, remove HTTP or apply URL Rewrite 301 to HTTPS; enable HSTS.
- NGINX: 80 → 301 to 443; enable HSTS; strong ciphers; HTTP/2/3 as needed.

---

## 14) Non‑Goals (Confirmed)
- No application code changes.
- No module refactors.
- No repository‑wide dependency updates.
