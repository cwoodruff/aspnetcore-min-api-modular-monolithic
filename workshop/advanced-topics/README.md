# Advanced Topics Implementation Guides

## ASP.NET Core Minimal API Workshop Extensions

This folder contains detailed implementation guides for advanced topics that can extend the core workshop or serve as standalone learning resources.

---

## Quick Start Additions (30 minutes or less)

These topics can be quickly added to the existing workshop solution:

| Guide | Time | Description |
|-------|------|-------------|
| [02 - Typed Results](02-TYPED-RESULTS.md) | 30 min | Better OpenAPI documentation with compile-time safety |
| [10 - Compression](10-COMPRESSION.md) | 20 min | Response compression for performance |
| [04 - Health Checks](04-HEALTH-CHECKS.md) | 30 min | Production-ready health endpoints |

---

## Core Workshop Extensions (45-60 minutes each)

These topics extend the architecture patterns taught in the workshop:

| Guide | Time | Description |
|-------|------|-------------|
| [01 - Endpoint Filters](01-ENDPOINT-FILTERS.md) | 45 min | Cross-cutting concerns for Minimal APIs |
| [03 - Output Caching](03-OUTPUT-CACHING.md) | 30 min | HTTP-level caching to complement service caching |
| [05 - Structured Logging](05-STRUCTURED-LOGGING.md) | 45 min | Serilog, correlation IDs, centralized logging |
| [06 - API Versioning](06-API-VERSIONING.md) | 30 min | Managing API evolution |
| [09 - Feature Flags](09-FEATURE-FLAGS.md) | 30 min | Toggle features without deployment |

---

## Advanced Architecture Patterns (60-90 minutes each)

These topics introduce significant architectural patterns:

| Guide | Time | Description |
|-------|------|-------------|
| [07 - Background Services](07-BACKGROUND-SERVICES.md) | 45 min | Async processing, queues, scheduled tasks |
| [08 - OpenTelemetry](08-OPENTELEMETRY.md) | 45 min | Distributed tracing and observability |
| [11 - CQRS with MediatR](11-CQRS-MEDIATR.md) | 60 min | Command/Query separation pattern |
| [14 - Outbox Pattern](14-OUTBOX-PATTERN.md) | 60 min | Reliable messaging in distributed systems |

---

## Integration Technologies (45-60 minutes each)

These topics add new communication capabilities:

| Guide | Time | Description |
|-------|------|-------------|
| [12 - SignalR](12-SIGNALR.md) | 45 min | Real-time bidirectional communication |
| [13 - gRPC](13-GRPC.md) | 45 min | High-performance RPC for service-to-service |

---

## Guide Contents

Each guide includes:

- **Overview** — What the technology does and why it matters
- **Learning Objectives** — What you'll be able to do after completing
- **Step-by-step Implementation** — Code samples you can copy
- **Configuration Examples** — appsettings.json and Program.cs snippets
- **Best Practices** — Production recommendations
- **Testing** — How to verify your implementation
- **Summary** — Quick reference tables

---

## Suggested Learning Paths

### Path 1: Production-Ready API
For teams preparing to deploy:
1. Health Checks (04)
2. Structured Logging (05)
3. Compression (10)
4. OpenTelemetry (08)

### Path 2: Clean Architecture
For teams focused on maintainability:
1. Endpoint Filters (01)
2. Typed Results (02)
3. CQRS with MediatR (11)
4. API Versioning (06)

### Path 3: Real-Time Features
For applications needing live updates:
1. SignalR (12)
2. Background Services (07)
3. Feature Flags (09)

### Path 4: Microservices Preparation
For teams planning to scale:
1. gRPC (13)
2. Outbox Pattern (14)
3. OpenTelemetry (08)
4. Output Caching (03)

---

## Prerequisites by Guide

| Guide | NuGet Packages Required |
|-------|------------------------|
| 01 - Endpoint Filters | (none - built-in) |
| 02 - Typed Results | (none - built-in) |
| 03 - Output Caching | (none - built-in) |
| 04 - Health Checks | `AspNetCore.HealthChecks.*` |
| 05 - Structured Logging | `Serilog.AspNetCore` |
| 06 - API Versioning | `Asp.Versioning.Http` |
| 07 - Background Services | (none - built-in) |
| 08 - OpenTelemetry | `OpenTelemetry.*` |
| 09 - Feature Flags | `Microsoft.FeatureManagement.AspNetCore` |
| 10 - Compression | (none - built-in) |
| 11 - CQRS/MediatR | `MediatR` |
| 12 - SignalR | (none - built-in) |
| 13 - gRPC | `Grpc.AspNetCore` |
| 14 - Outbox Pattern | (varies by broker) |

---

## Integration with Workshop Solution

These guides are designed to work with the Modular Monolith workshop solution. Most can be added incrementally without disrupting existing functionality.

### Recommended Integration Order

```
Workshop Solution (Base)
    │
    ├── Add Typed Results (improves OpenAPI)
    │
    ├── Add Endpoint Filters (improves validation)
    │
    ├── Add Health Checks (improves operations)
    │
    ├── Add Structured Logging (improves debugging)
    │
    └── Choose path based on needs:
        ├── Performance: Output Caching, Compression
        ├── Architecture: CQRS, API Versioning
        └── Scale: gRPC, Outbox Pattern
```

---

## File Organization

```
workshop/advanced-topics/
├── README.md                    (this file)
├── 01-ENDPOINT-FILTERS.md
├── 02-TYPED-RESULTS.md
├── 03-OUTPUT-CACHING.md
├── 04-HEALTH-CHECKS.md
├── 05-STRUCTURED-LOGGING.md
├── 06-API-VERSIONING.md
├── 07-BACKGROUND-SERVICES.md
├── 08-OPENTELEMETRY.md
├── 09-FEATURE-FLAGS.md
├── 10-COMPRESSION.md
├── 11-CQRS-MEDIATR.md
├── 12-SIGNALR.md
├── 13-GRPC.md
└── 14-OUTBOX-PATTERN.md
```

---

## Contributing

To add a new advanced topic guide:

1. Use the existing guides as a template
2. Include: Overview, Learning Objectives, Implementation, Testing, Summary
3. Number the guide sequentially (15-TOPIC-NAME.md)
4. Update this README with the new guide
5. Add to appropriate learning path(s)

---

## Version Compatibility

These guides are written for:
- **.NET 10**
- **ASP.NET Core 10**
- **C# 13**

Most patterns work with .NET 8+ with minor adjustments.
