# History - Ukaylee

## Project Context
- Project: aspnetcore-min-api-modular-monolithic (ASP.NET Core 10, Minimal APIs, modular monolith)
- Modules: Music, Orders, Admin, Reporting, Identity
- Stack: EF Core, FluentValidation, SQLite, xUnit, WebApplicationFactory, Docker
- User: Chris Woodruff
- Team formed: 2026-07-18T15:23:33-04:00

- 2026-07-18T15:51:51-04:00: Removed hard-coded demo identity credentials, moved in-memory users to configuration, and gated the in-memory auth store to Development/Demo only.
- 2026-07-18T16:05:29-04:00: Expanded `README.md` user account documentation for the Identity module, covering account fields, authorization policy mapping, user-secrets examples, and non-production security caveats.
- 2026-07-18T16:13:46-04:00: Centralized API ProblemDetails handling for FluentValidation and malformed request bodies, with regression assertions in `ErrorScenarioTests.cs` and full build/test validation passing (179/179).
- 2026-07-19T07:38:03-04:00: Fixed the Identity in-memory user config binding so Development/Demo logins work for configured users again, added startup diagnostics for disabled or incomplete user configuration, updated README environment guidance, and added regression coverage for config-driven login.
