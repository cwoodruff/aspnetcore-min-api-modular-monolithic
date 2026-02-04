# ASP.NET Core Minimal APIs: Online Course

## Course Overview

**Title:** Building Production-Ready APIs with ASP.NET Core Minimal APIs
**Subtitle:** Master Modular Monolithic Architecture from Zero to Production

**Format:** Self-paced video course
**Total Videos:** 52 lessons
**Total Duration:** ~10 hours
**Skill Level:** Intermediate to Advanced
**Prerequisites:** C# fundamentals, basic HTTP/REST knowledge, familiarity with
ASP.NET Core

---

## Course Structure

| Module    | Title                            | Videos | Duration    |
|-----------|----------------------------------|--------|-------------|
| 1         | Getting Started                  | 5      | 45 min      |
| 2         | Architecture & Module Contract   | 6      | 55 min      |
| 3         | Building Your First Module       | 6      | 60 min      |
| 4         | Authentication & Authorization   | 8      | 85 min      |
| 5         | Data Access & Repository Pattern | 7      | 70 min      |
| 6         | Caching & Performance            | 7      | 65 min      |
| 7         | Validation & Security            | 7      | 65 min      |
| 8         | Production Readiness             | 6      | 70 min      |
| 9         | Advanced Patterns                | 6      | 65 min      |
| **Total** |                                  | **58** | **~10 hrs** |

---

# Module 1: Getting Started

**Module Duration:** 45 minutes
**Learning Goal:** Set up your development environment and understand the course
project

## Videos

### 1.1 Course Introduction

**Duration:** 5 min | **Type:** Lecture

- Welcome and course overview
- What you'll build: 5 production-ready modules
- How to get the most from this course
- Community and support resources

### 1.2 Prerequisites and Tools

**Duration:** 8 min | **Type:** Lecture

- Required software versions (.NET 10, VS Code/Visual Studio, Git)
- Recommended VS Code extensions
- SQLite tools overview
- Docker (optional) for containerized development

### 1.3 Environment Setup

**Duration:** 10 min | **Type:** Hands-On

- Installing .NET SDK
- Verifying installation (`dotnet --version`)
- Cloning the course repository
- Running initial build verification

### 1.4 Solution Structure Walkthrough

**Duration:** 12 min | **Type:** Code Tour

- Project organization overview
- Host project: `ModularMonolith.Api`
- Module projects: Music, Orders, Administration, Reporting, Identity
- SharedKernel projects and their purposes
- Test projects structure

### 1.5 Running the API

**Duration:** 10 min | **Type:** Hands-On

- Starting the API with `dotnet run`
- Exploring Swagger UI
- Testing endpoints with curl/Postman
- Understanding the Chinook sample database

**Module Resources:**

- Setup checklist PDF
- Troubleshooting guide
- Quick reference card

---

# Module 2: Architecture & Module Contract

**Module Duration:** 55 minutes
**Learning Goal:** Understand modular monolithic architecture and the IModule
contract pattern

## Videos

### 2.1 Why Modular Monolith?

**Duration:** 10 min | **Type:** Lecture

- Traditional monolith challenges
- Microservices complexity and when it's overkill
- Modular monolith: the middle ground
- When to choose each architecture

### 2.2 Architecture Comparison Deep Dive

**Duration:** 8 min | **Type:** Lecture

- Deployment characteristics comparison
- Team scalability considerations
- Data management strategies
- Communication patterns

### 2.3 The IModule Contract

**Duration:** 12 min | **Type:** Code Walkthrough

- Interface definition and purpose
- `Name` property for module identification
- `RegisterServices()` for dependency injection
- `MapEndpoints()` for route registration
- Why this pattern enables clean boundaries

### 2.4 Host Composition Flow

**Duration:** 10 min | **Type:** Code Walkthrough

- How `Program.cs` discovers modules
- Service registration order and importance
- Middleware pipeline configuration
- Endpoint mapping sequence

### 2.5 Module Boundaries and Dependencies

**Duration:** 8 min | **Type:** Lecture

- Internal vs public types
- Cross-module communication patterns
- Shared kernel usage guidelines
- Avoiding tight coupling

### 2.6 Hands-On: Exploring Module Structure

**Duration:** 7 min | **Type:** Exercise

- Navigate the Music module
- Identify the module contract implementation
- Trace service registration
- Follow endpoint mapping

**Module Resources:**

- Architecture decision record template
- Module boundary checklist
- Diagram: Module composition flow

---

# Module 3: Building Your First Module

**Module Duration:** 60 minutes
**Learning Goal:** Create endpoints with proper metadata and OpenAPI
documentation

## Videos

### 3.1 Minimal API Fundamentals

**Duration:** 8 min | **Type:** Lecture

- Minimal APIs vs Controllers
- Lambda-based routing
- Route parameters and query strings
- Request/response handling

### 3.2 Creating Health Endpoints

**Duration:** 12 min | **Type:** Code-Along

- Basic health endpoint implementation
- Returning status information
- JSON response formatting
- Route grouping with `MapGroup()`

### 3.3 Endpoint Metadata

**Duration:** 10 min | **Type:** Code Walkthrough

- `WithName()` for operation IDs
- `WithTags()` for Swagger grouping
- `WithDescription()` and `WithSummary()`
- `Produces<T>()` for response documentation

### 3.4 Typed Results for Better APIs

**Duration:** 12 min | **Type:** Code-Along

- Problem with untyped results
- `TypedResults` class introduction
- `Results<T1, T2>` union types
- Compile-time safety benefits
- OpenAPI schema generation

### 3.5 Extension Method Pattern

**Duration:** 10 min | **Type:** Code Walkthrough

- Organizing endpoints into classes
- Extension methods for `IEndpointRouteBuilder`
- Keeping modules clean and maintainable
- Naming conventions

### 3.6 Hands-On: Build a Complete Endpoint

**Duration:** 8 min | **Type:** Exercise

- Create a new endpoint from scratch
- Add proper metadata
- Use typed results
- Verify in Swagger UI

**Module Resources:**

- Endpoint metadata cheat sheet
- TypedResults quick reference
- Exercise solution code

---

# Module 4: Authentication & Authorization

**Module Duration:** 85 minutes
**Learning Goal:** Implement JWT authentication with policy-based authorization

## Videos

### 4.1 Authentication Overview

**Duration:** 8 min | **Type:** Lecture

- Authentication vs authorization
- Token-based authentication benefits
- JWT structure and claims
- RS256 vs HS256 signing

### 4.2 JWT Token Anatomy

**Duration:** 10 min | **Type:** Lecture

- Header, payload, signature breakdown
- Standard claims (sub, iss, aud, exp)
- Custom claims for permissions
- Token validation process

### 4.3 Key Material Service

**Duration:** 12 min | **Type:** Code Walkthrough

- RSA key pair generation
- Key storage considerations
- JWKS endpoint purpose
- Key rotation strategies

### 4.4 Token Service Implementation

**Duration:** 15 min | **Type:** Code-Along

- Creating access tokens
- Setting expiration and claims
- Refresh token implementation
- Token response structure

### 4.5 Authentication Endpoints

**Duration:** 12 min | **Type:** Code-Along

- Login endpoint implementation
- Refresh token endpoint
- Logout and token revocation
- User info endpoint

### 4.6 Policy-Based Authorization

**Duration:** 12 min | **Type:** Code Walkthrough

- Defining authorization policies
- Permission-based policies
- Role-based policies
- Custom authorization requirements

### 4.7 Multi-Tenant Authorization

**Duration:** 10 min | **Type:** Code Walkthrough

- Tenant header validation
- `TenantAuthorizationHandler` implementation
- Scoping data by tenant
- Tenant claim extraction

### 4.8 Hands-On: Secure an Endpoint

**Duration:** 6 min | **Type:** Exercise

- Add authentication requirement
- Apply authorization policy
- Test with demo users
- Verify access control

**Module Resources:**

- JWT debugging guide
- Policy configuration reference
- Demo user credentials table

---

# Module 5: Data Access & Repository Pattern

**Module Duration:** 70 minutes
**Learning Goal:** Implement clean data access with Entity Framework Core and
repositories

## Videos

### 5.1 Repository Pattern Overview

**Duration:** 8 min | **Type:** Lecture

- Why use repositories?
- Abstraction over EF Core
- Testability benefits
- When to use DbContext directly

### 5.2 Entity Framework Core Setup

**Duration:** 10 min | **Type:** Code Walkthrough

- `AppDbContext` configuration
- Entity mapping with Fluent API
- DbContext pooling benefits
- Connection string management

### 5.3 Base Repository Implementation

**Duration:** 12 min | **Type:** Code-Along

- Generic CRUD operations
- `GetByIdAsync()` implementation
- `GetAllAsync()` with pagination
- `AddAsync()`, `UpdateAsync()`, `DeleteAsync()`

### 5.4 Entity-Specific Repositories

**Duration:** 12 min | **Type:** Code-Along

- Extending base repository
- Custom query methods
- Eager loading with `Include()`
- Filtering and sorting

### 5.5 Complex Query Patterns

**Duration:** 10 min | **Type:** Code Walkthrough

- Split queries for performance
- Projection with `Select()`
- Async enumeration
- Query optimization tips

### 5.6 Endpoint Filters

**Duration:** 12 min | **Type:** Code-Along

- Filter pipeline concept
- Validation filters
- Logging filters
- Exception handling filters
- Filter ordering

### 5.7 Hands-On: Create a Repository

**Duration:** 6 min | **Type:** Exercise

- Implement a new entity repository
- Add custom query method
- Register in DI container
- Use in an endpoint

**Module Resources:**

- EF Core query patterns guide
- Repository interface templates
- Filter implementation examples

---

# Module 6: Caching & Performance

**Module Duration:** 65 minutes
**Learning Goal:** Implement multi-level caching for optimal API performance

## Videos

### 6.1 Caching Strategy Overview

**Duration:** 8 min | **Type:** Lecture

- Why caching matters
- Cache-aside pattern explained
- L1 (in-memory) vs L2 (distributed) caching
- Cache invalidation challenges

### 6.2 ICacheFacade Interface

**Duration:** 10 min | **Type:** Code Walkthrough

- Facade pattern for caching
- `GetOrAddAsync()` method
- Cache entry options
- Provider abstraction

### 6.3 Cache Key Composition

**Duration:** 10 min | **Type:** Code Walkthrough

- `CacheKeyComposer` purpose
- Key structure: environment, app, module, entity, version
- Discriminator for uniqueness
- Avoiding key collisions

### 6.4 Implementing Cache-Aside Pattern

**Duration:** 12 min | **Type:** Code-Along

- Service layer integration
- Reading through cache
- Cache miss handling
- Entry expiration with jitter

### 6.5 Tag-Based Cache Invalidation

**Duration:** 10 min | **Type:** Code-Along

- Tagging cache entries
- `RemoveByTagAsync()` implementation
- Invalidation on mutations
- Cascading invalidation patterns

### 6.6 Output Caching

**Duration:** 8 min | **Type:** Code Walkthrough

- HTTP-level caching
- `OutputCache` middleware
- Cache policies
- Combining with service caching

### 6.7 Response Compression

**Duration:** 7 min | **Type:** Code Walkthrough

- Compression middleware setup
- Gzip and Brotli providers
- When to compress
- Performance measurements

**Module Resources:**

- Caching decision flowchart
- Cache key naming conventions
- Performance benchmarks

---

# Module 7: Validation & Security

**Module Duration:** 65 minutes
**Learning Goal:** Implement input validation and security hardening

## Videos

### 7.1 FluentValidation Introduction

**Duration:** 8 min | **Type:** Lecture

- Why FluentValidation over Data Annotations
- Fluent rule syntax
- Separation of concerns
- Reusable validators

### 7.2 Creating Validators

**Duration:** 12 min | **Type:** Code-Along

- Validator class structure
- Built-in rules: `NotEmpty()`, `MaxLength()`, `EmailAddress()`
- Custom validation rules
- Conditional validation with `When()`

### 7.3 Validator Registration and Usage

**Duration:** 10 min | **Type:** Code-Along

- Registering validators in DI
- Service layer validation integration
- Validation result handling
- Early return on failure

### 7.4 ProblemDetails Error Responses

**Duration:** 8 min | **Type:** Code Walkthrough

- RFC 7807 problem details format
- Mapping validation errors
- Consistent error structure
- Client-friendly error messages

### 7.5 Rate Limiting Configuration

**Duration:** 10 min | **Type:** Code-Along

- Rate limiter middleware setup
- Fixed window limiter explained
- Policy definitions
- Partition keys for user/tenant scoping

### 7.6 Security Headers and CORS

**Duration:** 10 min | **Type:** Code Walkthrough

- HSTS configuration
- Content Security Policy basics
- CORS policy setup
- Allowed origins configuration

### 7.7 Hands-On: Add Validation

**Duration:** 7 min | **Type:** Exercise

- Create a validator for existing model
- Integrate with service
- Test validation errors
- Verify ProblemDetails response

**Module Resources:**

- FluentValidation rules reference
- Security headers checklist
- Rate limiting policy examples

---

# Module 8: Production Readiness

**Module Duration:** 70 minutes
**Learning Goal:** Prepare your API for production with health checks, logging,
and observability

## Videos

### 8.1 Health Checks Deep Dive

**Duration:** 12 min | **Type:** Code-Along

- Built-in health check middleware
- Database health checks
- Custom health check implementations
- Health check UI options
- Kubernetes readiness/liveness probes

### 8.2 Structured Logging with Serilog

**Duration:** 15 min | **Type:** Code-Along

- Serilog setup and configuration
- Structured log properties
- Log sinks: Console, File, Seq
- Log levels and filtering
- Performance considerations

### 8.3 Correlation IDs

**Duration:** 10 min | **Type:** Code Walkthrough

- Request correlation concept
- Middleware implementation
- Propagating across services
- Including in log context

### 8.4 OpenTelemetry Fundamentals

**Duration:** 15 min | **Type:** Code-Along

- Traces, metrics, and logs
- Instrumentation setup
- Exporter configuration (Jaeger, OTLP)
- Custom spans and attributes
- Dashboard visualization

### 8.5 API Versioning Strategies

**Duration:** 10 min | **Type:** Code Walkthrough

- URL path versioning
- Query string versioning
- Header versioning
- Deprecation workflows
- Version documentation

### 8.6 Feature Flags

**Duration:** 8 min | **Type:** Code Walkthrough

- Feature management setup
- Flag evaluation in code
- Gradual rollouts
- A/B testing patterns

**Module Resources:**

- Health check implementation guide
- Serilog configuration templates
- OpenTelemetry setup checklist

---

# Module 9: Advanced Patterns

**Module Duration:** 65 minutes
**Learning Goal:** Explore advanced architectural patterns for scaling your
application

## Videos

### 9.1 Background Services

**Duration:** 12 min | **Type:** Code-Along

- `IHostedService` and `BackgroundService`
- Scheduled task implementation
- Queue processing patterns
- Graceful shutdown handling

### 9.2 Integration Testing Fundamentals

**Duration:** 12 min | **Type:** Code-Along

- `WebApplicationFactory` setup
- Test client configuration
- Database isolation strategies
- Test authentication helpers

### 9.3 Testing Authenticated Endpoints

**Duration:** 10 min | **Type:** Code-Along

- Generating test tokens
- `WithTenantUser()` helper
- Authorization policy testing
- Asserting response codes

### 9.4 CQRS Pattern Introduction

**Duration:** 12 min | **Type:** Lecture + Demo

- Command Query Responsibility Segregation
- MediatR integration overview
- Commands vs queries
- When CQRS adds value

### 9.5 Real-Time with SignalR

**Duration:** 10 min | **Type:** Lecture + Demo

- SignalR hub basics
- Client-server communication
- Integration with Minimal APIs
- Use cases: notifications, live updates

### 9.6 Course Wrap-Up

**Duration:** 9 min | **Type:** Lecture

- Key concepts review
- Architecture decision guide
- Next steps and resources
- Community and continued learning
- Self-study topics: gRPC, Outbox Pattern

**Module Resources:**

- Integration test templates
- CQRS decision guide
- Complete course code repository

---

# Bonus Content (Self-Study)

## Bonus 1: gRPC for Service Communication

**Duration:** 45 min | **Type:** Self-Paced

- Protocol Buffers basics
- gRPC service definition
- Integration with Minimal API host
- When to use gRPC vs REST

## Bonus 2: Outbox Pattern for Reliability

**Duration:** 60 min | **Type:** Self-Paced

- Transactional outbox concept
- Implementation with EF Core
- Message publishing
- Idempotency considerations

## Bonus 3: CQRS with MediatR Deep Dive

**Duration:** 60 min | **Type:** Self-Paced

- Full MediatR setup
- Command handlers
- Query handlers
- Pipeline behaviors

---

# Course Materials Summary

## Downloadable Resources

| Resource              | Format  | Description                        |
|-----------------------|---------|------------------------------------|
| Complete Source Code  | GitHub  | All modules with solution branches |
| Quick Reference Cards | PDF     | Cheat sheets for each module       |
| Architecture Diagrams | PNG/PDF | Visual guides for patterns         |
| Exercise Solutions    | GitHub  | Step-by-step solutions             |
| Slide Decks           | PDF     | Lecture slides for reference       |

## Community Access

- Discord server for Q&A
- Monthly live Q&A sessions
- Code review opportunities
- Project showcase channel

---

# Video Production Notes

## Video Types

| Type                 | Format                   | Notes                           |
|----------------------|--------------------------|---------------------------------|
| **Lecture**          | Slides + talking head    | Conceptual explanations         |
| **Code Walkthrough** | Screen share + voiceover | Explaining existing code        |
| **Code-Along**       | Screen share + typing    | Building code step-by-step      |
| **Exercise**         | Instructions + hints     | Learner practices independently |
| **Demo**             | Screen share             | Showing working features        |

## Recording Guidelines

- Resolution: 1920x1080 minimum
- Audio: Clear, noise-free
- Code font size: 16pt minimum
- IDE theme: High contrast (dark preferred)
- Terminal: Visible, readable output

## Post-Production

- Captions/subtitles for accessibility
- Chapter markers for navigation
- Downloadable transcripts
- Speed adjustment (0.5x - 2x)

---

# Course Metadata

**Platform Compatibility:**

- Udemy, Teachable, Thinkific, Pluralsight
- SCORM compliant for LMS integration

**Pricing Tiers:**

- Basic: Video access only
- Standard: Videos + resources + community
- Premium: Standard + live Q&A + code review

**Certification:**

- Quiz per module (optional)
- Final project submission
- Certificate of completion

---

*Course outline created: February 2026*
*Based on: ASP.NET Core 10 Minimal APIs Workshop*
