# ASP.NET Core Minimal APIs: Complete Online Course

## Course Overview

**Title:** Building Production-Ready APIs with ASP.NET Core Minimal APIs
**Subtitle:** Master Modular Monolithic Architecture from Zero to Enterprise
Scale

**Format:** Self-paced video course
**Total Videos:** 132 lessons
**Total Duration:** ~20 hours
**Skill Level:** Intermediate to Advanced
**Prerequisites:** C# fundamentals, basic HTTP/REST knowledge, familiarity with
ASP.NET Core

---

## What You'll Learn

By the end of this course, you will be able to:

- Design and implement modular monolithic architectures
- Build secure, scalable REST APIs with ASP.NET Core Minimal APIs
- Implement enterprise-grade authentication and authorization
- Apply advanced data access patterns for high-performance applications
- Configure comprehensive observability, logging, and monitoring
- Deploy cloud-native applications with CI/CD pipelines
- Make informed architectural decisions for production systems
- Implement security best practices and compliance requirements

---

## Course Structure

| Module    | Title                                 | Videos  | Duration      |
|-----------|---------------------------------------|---------|---------------|
| 1         | Getting Started                       | 7       | 60 min        |
| 2         | Architecture & Module Contract        | 6       | 55 min        |
| 3         | Building Your First Module            | 6       | 60 min        |
| 4         | Authentication & Authorization        | 10      | 105 min       |
| 5         | Data Access & Repository Pattern      | 7       | 70 min        |
| 6         | Caching & Performance                 | 9       | 85 min        |
| 7         | Validation & Security                 | 10      | 85 min        |
| 8         | Production Readiness                  | 8       | 90 min        |
| 9         | Advanced Patterns                     | 8       | 85 min        |
| 10        | Enterprise Data Access Patterns       | 8       | 95 min        |
| 11        | Advanced Security & Compliance        | 9       | 105 min       |
| 12        | Observability & Operations            | 9       | 105 min       |
| 13        | Cloud-Native Deployment               | 8       | 100 min       |
| 14        | Architecture Decisions & Case Studies | 8       | 90 min        |
| **Total** |                                       | **113** | **~19.5 hrs** |

**Bonus Content:** 5 self-study modules (~3 hours additional)

---

# PART 1: FOUNDATIONS

---

# Module 1: Getting Started

**Module Duration:** 60 minutes
**Learning Goal:** Set up your development environment and understand the course
project

## Videos

### 1.1 Course Introduction

**Duration:** 5 min | **Type:** Lecture

- Welcome and course overview
- What you'll build: 5 production-ready modules
- Learning paths and recommendations
- Community and support resources

### 1.2 Prerequisites and Tools

**Duration:** 8 min | **Type:** Lecture

- Required software versions (.NET 10, VS Code/Visual Studio, Git)
- Recommended VS Code extensions
- SQLite tools overview
- Optional tools: Docker, Postman, Azure Data Studio

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

### 1.6 Docker & Containerization Basics

**Duration:** 10 min | **Type:** Code-Along

- Understanding the Dockerfile
- Building container images
- Running with Docker Compose
- Container networking basics
- Development vs production containers

### 1.7 Development Workflow & Debugging

**Duration:** 5 min | **Type:** Tips

- Hot reload configuration
- Debugging in VS Code and Visual Studio
- Launch settings and profiles
- Useful CLI commands

**Module Resources:**

- Setup checklist PDF
- Troubleshooting guide
- Quick reference card
- Docker cheat sheet

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
- Cost implications

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

# PART 2: CORE PATTERNS

---

# Module 4: Authentication & Authorization

**Module Duration:** 105 minutes
**Learning Goal:** Implement JWT authentication with policy-based authorization
and external providers

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

### 4.8 External Identity Providers

**Duration:** 12 min | **Type:** Code-Along

- OAuth2 and OpenID Connect overview
- Integrating Azure AD / Entra ID
- Google and GitHub OAuth
- Social login implementation
- Mapping external claims to local users

### 4.9 Refresh Token Security

**Duration:** 8 min | **Type:** Lecture

- Refresh token rotation
- Token families and revocation
- Detecting token reuse attacks
- Secure storage strategies

### 4.10 Hands-On: Secure an Endpoint

**Duration:** 6 min | **Type:** Exercise

- Add authentication requirement
- Apply authorization policy
- Test with demo users
- Verify access control

**Module Resources:**

- JWT debugging guide
- Policy configuration reference
- Demo user credentials table
- OAuth provider setup guides

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

**Module Duration:** 85 minutes
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

### 6.8 Distributed Caching with Redis

**Duration:** 10 min | **Type:** Code-Along

- Redis setup and configuration
- `IDistributedCache` implementation
- Connection management
- Failover strategies
- Cache warming patterns

### 6.9 Database Query Caching

**Duration:** 10 min | **Type:** Code Walkthrough

- Second-level cache concepts
- EF Core query caching strategies
- Compiled queries
- Query plan caching
- Performance benchmarks

**Module Resources:**

- Caching decision flowchart
- Cache key naming conventions
- Performance benchmarks
- Redis configuration templates

---

# Module 7: Validation & Security

**Module Duration:** 85 minutes
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

### 7.7 Input Sanitization & XSS Prevention

**Duration:** 10 min | **Type:** Code-Along

- Understanding XSS attack vectors
- HTML encoding and escaping
- Content Security Policy headers
- Sanitizing user input
- Client-side vs server-side validation

### 7.8 CSRF Protection

**Duration:** 5 min | **Type:** Lecture

- CSRF attack explanation
- Anti-forgery tokens
- SameSite cookie attribute
- When CSRF protection is needed

### 7.9 API Key Authentication

**Duration:** 5 min | **Type:** Code Walkthrough

- API key generation patterns
- Header vs query parameter
- Key rotation strategies
- Scoping keys to resources

### 7.10 Hands-On: Add Validation

**Duration:** 7 min | **Type:** Exercise

- Create a validator for existing model
- Integrate with service
- Test validation errors
- Verify ProblemDetails response

**Module Resources:**

- FluentValidation rules reference
- Security headers checklist
- Rate limiting policy examples
- OWASP quick reference

---

# Module 8: Production Readiness

**Module Duration:** 90 minutes
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

### 8.7 Deployment Strategies

**Duration:** 12 min | **Type:** Lecture

- Blue-green deployments
- Canary releases
- Rolling updates
- Rollback procedures
- Zero-downtime deployments

### 8.8 Configuration Management

**Duration:** 8 min | **Type:** Code Walkthrough

- Environment-specific configuration
- Configuration providers hierarchy
- Secrets injection at runtime
- Options pattern best practices
- Configuration validation

**Module Resources:**

- Health check implementation guide
- Serilog configuration templates
- OpenTelemetry setup checklist
- Deployment strategy comparison

---

# PART 3: ADVANCED PATTERNS

---

# Module 9: Advanced Integration Patterns

**Module Duration:** 85 minutes
**Learning Goal:** Implement advanced integration and messaging patterns

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

### 9.6 Webhook Implementation

**Duration:** 10 min | **Type:** Code-Along

- Webhook design patterns
- Delivery guarantees and retry logic
- Signature verification
- Webhook security best practices
- Testing webhooks locally

### 9.7 Idempotency & Duplicate Detection

**Duration:** 10 min | **Type:** Code Walkthrough

- Why idempotency matters
- Idempotency key patterns
- Request deduplication strategies
- Database-level constraints
- Handling concurrent requests

### 9.8 Course Progress: What's Next

**Duration:** 9 min | **Type:** Lecture

- Review of patterns learned
- Transition to enterprise topics
- Recommended learning paths

**Module Resources:**

- Integration test templates
- CQRS decision guide
- Webhook security checklist
- Idempotency implementation guide

---

# PART 4: ENTERPRISE SCALE

---

# Module 10: Enterprise Data Access Patterns

**Module Duration:** 95 minutes
**Learning Goal:** Implement advanced data access patterns for high-performance
enterprise applications

## Videos

### 10.1 Query Performance & Optimization

**Duration:** 12 min | **Type:** Code-Along

- Identifying N+1 query problems
- Using `AsSplitQuery()` effectively
- Lazy loading traps and solutions
- Query execution plans
- Database profiling tools

### 10.2 Pagination at Scale

**Duration:** 12 min | **Type:** Code-Along

- Offset vs cursor-based pagination
- Keyset pagination implementation
- Performance implications
- Handling large datasets
- API design for pagination

### 10.3 Soft Deletes & Audit Trails

**Duration:** 12 min | **Type:** Code-Along

- Soft delete pattern implementation
- Global query filters
- Audit trail with created/modified tracking
- `SaveChanges` interception
- Audit log storage strategies

### 10.4 Specification Pattern

**Duration:** 12 min | **Type:** Code-Along

- Specification pattern explained
- Building composable queries
- Dynamic filtering
- Reusable query specifications
- Integration with repositories

### 10.5 Optimistic Concurrency

**Duration:** 12 min | **Type:** Code Walkthrough

- Concurrency conflicts explained
- Row version / timestamp columns
- Handling `DbUpdateConcurrencyException`
- Conflict resolution strategies
- User experience considerations

### 10.6 Bulk Operations

**Duration:** 15 min | **Type:** Code-Along

- EF Core bulk insert patterns
- `ExecuteUpdate` and `ExecuteDelete`
- Third-party bulk libraries
- Performance comparisons
- When to bypass EF Core

### 10.7 Multi-Database Strategies

**Duration:** 10 min | **Type:** Lecture

- Read replicas and write splitting
- Database per tenant
- Polyglot persistence
- Connection management
- Transaction considerations

### 10.8 Hands-On: Advanced Repository

**Duration:** 10 min | **Type:** Exercise

- Implement specification pattern
- Add soft delete support
- Configure audit tracking
- Test with concurrent updates

**Module Resources:**

- Query optimization checklist
- Pagination decision tree
- Audit trail implementation guide
- Bulk operations benchmark data

---

# Module 11: Advanced Security & Compliance

**Module Duration:** 105 minutes
**Learning Goal:** Implement enterprise security patterns and compliance
requirements

## Videos

### 11.1 OAuth2 & OpenID Connect Deep Dive

**Duration:** 15 min | **Type:** Lecture

- Authorization code flow with PKCE
- Implicit flow deprecation
- Scopes and consent
- Token introspection
- Federation scenarios

### 11.2 Claims Transformation

**Duration:** 12 min | **Type:** Code-Along

- `IClaimsTransformation` implementation
- Dynamic claim enrichment
- External claim mapping
- Claims caching strategies
- Performance considerations

### 11.3 Secrets Management

**Duration:** 12 min | **Type:** Code-Along

- Azure Key Vault integration
- AWS Secrets Manager
- HashiCorp Vault basics
- .NET User Secrets for development
- Secret rotation automation

### 11.4 SQL Injection Prevention

**Duration:** 10 min | **Type:** Code Walkthrough

- Parameterized queries in EF Core
- Raw SQL safety
- Dynamic query risks
- Stored procedure considerations
- Security testing techniques

### 11.5 OWASP API Security Top 10

**Duration:** 15 min | **Type:** Lecture

- Broken Object Level Authorization
- Broken Authentication
- Excessive Data Exposure
- Lack of Resources & Rate Limiting
- Broken Function Level Authorization
- Mass Assignment
- Security Misconfiguration
- Injection
- Improper Assets Management
- Insufficient Logging & Monitoring

### 11.6 Data Encryption

**Duration:** 12 min | **Type:** Code-Along

- Encryption at rest with EF Core
- Value converters for encryption
- Key management
- TLS/HTTPS enforcement
- Certificate management

### 11.7 Audit Logging for Compliance

**Duration:** 12 min | **Type:** Code-Along

- What to audit (access, changes, failures)
- Immutable audit logs
- GDPR considerations
- HIPAA requirements overview
- SOC 2 logging requirements

### 11.8 Advanced Rate Limiting

**Duration:** 10 min | **Type:** Code Walkthrough

- Distributed rate limiting with Redis
- Sliding window algorithm
- Token bucket pattern
- Quota management per tenant
- Rate limit headers and responses

### 11.9 Hands-On: Security Audit

**Duration:** 7 min | **Type:** Exercise

- Review endpoint security
- Implement audit logging
- Test rate limiting
- Verify encryption

**Module Resources:**

- OWASP API Security checklist
- Compliance mapping guide
- Secrets management comparison
- Security audit template

---

# Module 12: Observability & Operations

**Module Duration:** 105 minutes
**Learning Goal:** Implement comprehensive monitoring and operational practices

## Videos

### 12.1 Centralized Logging Architecture

**Duration:** 12 min | **Type:** Lecture + Demo

- ELK Stack overview (Elasticsearch, Logstash, Kibana)
- Azure Monitor / Application Insights
- Datadog integration
- Log aggregation patterns
- Retention policies

### 12.2 Distributed Tracing at Scale

**Duration:** 12 min | **Type:** Code-Along

- Jaeger advanced configuration
- Trace sampling strategies
- Span context propagation
- Cross-service tracing
- Trace analysis techniques

### 12.3 Custom Metrics & Instrumentation

**Duration:** 15 min | **Type:** Code-Along

- Prometheus metrics format
- Custom counters, gauges, histograms
- Business metrics tracking
- Grafana dashboard setup
- Alerting on metrics

### 12.4 Real-Time Alerting

**Duration:** 10 min | **Type:** Lecture

- Alert threshold design
- Anomaly detection basics
- PagerDuty / OpsGenie integration
- Alert fatigue prevention
- Runbook automation

### 12.5 Performance Profiling

**Duration:** 12 min | **Type:** Code-Along

- `dotnet-trace` and `dotnet-counters`
- Flame graph analysis
- BenchmarkDotNet for micro-benchmarks
- Memory profiling
- Setting performance baselines

### 12.6 Log Analysis & Troubleshooting

**Duration:** 10 min | **Type:** Demo

- Structured log queries
- Correlation ID tracing
- Root cause analysis workflow
- Common production issues
- Post-mortem templates

### 12.7 Dependency Health Monitoring

**Duration:** 12 min | **Type:** Code-Along

- Database connection monitoring
- Cache health checks
- External API health
- Circuit breaker status
- Dependency mapping

### 12.8 SLA & Error Budgets

**Duration:** 12 min | **Type:** Lecture

- Defining SLIs and SLOs
- Error budget calculation
- SLA reporting
- Incident tracking
- Continuous improvement

### 12.9 Hands-On: Monitoring Dashboard

**Duration:** 10 min | **Type:** Exercise

- Configure custom metrics
- Set up health checks
- Create Grafana dashboard
- Configure basic alerts

**Module Resources:**

- Observability maturity model
- Dashboard templates
- Alert rule examples
- Post-mortem template

---

# Module 13: Cloud-Native Deployment

**Module Duration:** 100 minutes
**Learning Goal:** Deploy and scale APIs in cloud environments

## Videos

### 13.1 Kubernetes Fundamentals

**Duration:** 15 min | **Type:** Lecture + Demo

- Pods, Services, Deployments explained
- ConfigMaps and Secrets
- Namespaces for environments
- kubectl essential commands
- Kubernetes dashboard

### 13.2 Container Best Practices

**Duration:** 12 min | **Type:** Code-Along

- Multi-stage Dockerfile optimization
- Image size reduction
- Security scanning
- Base image selection
- Layer caching strategies

### 13.3 Kubernetes Deployment Manifests

**Duration:** 12 min | **Type:** Code-Along

- Deployment configuration
- Resource limits and requests
- Liveness and readiness probes
- Horizontal Pod Autoscaler
- Rolling update configuration

### 13.4 CI/CD Pipelines

**Duration:** 15 min | **Type:** Code-Along

- GitHub Actions workflow
- Build, test, deploy stages
- Environment promotion
- Secret management in CI/CD
- Automated testing gates

### 13.5 Database Migrations in Production

**Duration:** 12 min | **Type:** Lecture

- EF Core migration strategies
- Zero-downtime migrations
- Rollback procedures
- Data seeding
- Schema versioning

### 13.6 Infrastructure as Code

**Duration:** 12 min | **Type:** Demo

- Terraform basics
- Azure Resource Manager templates
- Pulumi for .NET developers
- Environment reproducibility
- State management

### 13.7 Scaling Strategies

**Duration:** 12 min | **Type:** Lecture

- Horizontal vs vertical scaling
- Load balancer configuration
- Session affinity considerations
- Stateless API design
- Database scaling patterns

### 13.8 Hands-On: Deploy to Cloud

**Duration:** 10 min | **Type:** Exercise

- Build container image
- Push to registry
- Deploy to Kubernetes
- Verify health and scaling

**Module Resources:**

- Kubernetes manifest templates
- GitHub Actions workflow examples
- Terraform module templates
- Deployment checklist

---

# Module 14: Architecture Decisions & Case Studies

**Module Duration:** 90 minutes
**Learning Goal:** Make informed architectural decisions for production systems

## Videos

### 14.1 Monolith to Microservices

**Duration:** 12 min | **Type:** Lecture

- When to stay monolithic
- Extraction strategies
- Strangler fig pattern
- Cost-benefit analysis
- Migration timeline planning

### 14.2 Polyglot Persistence

**Duration:** 10 min | **Type:** Lecture

- Choosing the right database
- SQL vs NoSQL decisions
- Search engines (Elasticsearch)
- Time-series databases
- Data synchronization

### 14.3 Event-Driven Architecture

**Duration:** 12 min | **Type:** Lecture + Demo

- Events vs commands
- Event sourcing introduction
- Eventual consistency handling
- Message brokers (RabbitMQ, Azure Service Bus)
- Outbox pattern recap

### 14.4 API Gateway Pattern

**Duration:** 10 min | **Type:** Lecture

- Gateway responsibilities
- Rate limiting at gateway
- Authentication offloading
- Request routing
- API composition

### 14.5 Resilience Patterns

**Duration:** 12 min | **Type:** Code-Along

- Circuit breaker with Polly
- Retry policies
- Timeout handling
- Fallback strategies
- Bulkhead isolation

### 14.6 Cost Optimization

**Duration:** 10 min | **Type:** Lecture

- Cloud cost analysis
- Right-sizing resources
- Reserved instances
- Caching ROI
- Database optimization costs

### 14.7 Case Study: E-Commerce API

**Duration:** 12 min | **Type:** Lecture

- Requirements analysis
- Architecture decisions
- Technology choices
- Scaling considerations
- Lessons learned

### 14.8 Course Wrap-Up

**Duration:** 12 min | **Type:** Lecture

- Complete architecture review
- Key patterns summary
- Certification information
- Community resources
- Continuing education paths

**Module Resources:**

- Architecture decision records
- Pattern selection flowcharts
- Case study documentation
- Certification exam guide

---

# BONUS CONTENT (Self-Study)

---

## Bonus Module A: gRPC for Service Communication

**Duration:** 60 min | **Type:** Self-Paced | **Videos:** 5

### A.1 Protocol Buffers Fundamentals (12 min)

- Proto file syntax
- Message definitions
- Code generation
- Versioning considerations

### A.2 gRPC Service Implementation (15 min)

- Service definition
- Server implementation
- Client generation
- Error handling

### A.3 gRPC with Minimal APIs (12 min)

- Hosting gRPC alongside REST
- Shared models
- Authentication integration
- Health checks

### A.4 Streaming Patterns (12 min)

- Server streaming
- Client streaming
- Bidirectional streaming
- Use cases

### A.5 gRPC Best Practices (9 min)

- When to use gRPC vs REST
- Performance considerations
- Load balancing
- Deadline propagation

---

## Bonus Module B: Outbox Pattern Deep Dive

**Duration:** 60 min | **Type:** Self-Paced | **Videos:** 5

### B.1 Distributed Transaction Problems (12 min)

- Two-phase commit limitations
- Saga pattern overview
- Eventual consistency

### B.2 Outbox Pattern Implementation (15 min)

- Outbox table design
- Transactional writes
- Message publishing
- Duplicate handling

### B.3 Inbox Pattern (10 min)

- Idempotent consumers
- Inbox table design
- Processing guarantees

### B.4 Background Processing (12 min)

- Polling vs change data capture
- Retry strategies
- Dead letter handling

### B.5 Testing Outbox (11 min)

- Unit testing publishers
- Integration testing
- Failure scenarios

---

## Bonus Module C: CQRS with MediatR Advanced

**Duration:** 60 min | **Type:** Self-Paced | **Videos:** 5

### C.1 MediatR Deep Dive (12 min)

- Request/handler pattern
- Notification handlers
- Polymorphic dispatch

### C.2 Pipeline Behaviors (12 min)

- Cross-cutting concerns
- Validation behavior
- Logging behavior
- Performance monitoring

### C.3 Read Model Optimization (12 min)

- Separate read stores
- Denormalization strategies
- Cache integration

### C.4 Event Handlers (12 min)

- Domain events
- Integration events
- Event dispatching

### C.5 Testing CQRS (12 min)

- Testing handlers
- Mocking dependencies
- Integration scenarios

---

## Bonus Module D: Multi-Tenancy Architecture

**Duration:** 60 min | **Type:** Self-Paced | **Videos:** 5

### D.1 Multi-Tenancy Strategies (12 min)

- Database per tenant
- Schema per tenant
- Shared database with row-level
- Hybrid approaches

### D.2 Tenant Resolution (12 min)

- Header-based
- Subdomain-based
- Path-based
- Database routing

### D.3 Data Isolation (12 min)

- EF Core global filters
- Row-level security
- Testing isolation

### D.4 Tenant Configuration (12 min)

- Per-tenant settings
- Feature flags per tenant
- Custom branding

### D.5 Billing & Usage (12 min)

- Usage tracking
- Quota management
- Billing integration

---

## Bonus Module E: API Documentation Excellence

**Duration:** 45 min | **Type:** Self-Paced | **Videos:** 4

### E.1 OpenAPI Advanced Features (12 min)

- Schema customization
- Examples and descriptions
- Discriminators for polymorphism

### E.2 Documentation-Driven Development (10 min)

- Design-first approach
- Contract testing
- Mock servers

### E.3 Client SDK Generation (12 min)

- NSwag configuration
- OpenAPI Generator
- Versioned clients

### E.4 Interactive Documentation (11 min)

- Swagger UI customization
- ReDoc alternative
- Authentication in docs

---

# Course Materials Summary

## Downloadable Resources by Module

| Module       | Resources                                                     |
|--------------|---------------------------------------------------------------|
| All Modules  | Complete source code (GitHub)                                 |
| Module 1-3   | Setup guides, cheat sheets                                    |
| Module 4     | JWT debugging tools, OAuth guides                             |
| Module 5-6   | Query optimization guides, cache templates                    |
| Module 7     | Security checklists, OWASP guides                             |
| Module 8-9   | Deployment templates, test examples                           |
| Module 10-11 | Enterprise patterns, compliance guides                        |
| Module 12    | Dashboard templates, alert configurations                     |
| Module 13-14 | K8s manifests, CI/CD workflows, architecture decision records |

## Learning Paths

### Path A: API Developer (12 hours)

Modules 1-9 + select advanced topics
*Focus: Building and testing APIs*

### Path B: Enterprise Architect (16 hours)

Modules 1-14 (skip bonus)
*Focus: Production-ready architecture*

### Path C: Full Mastery (20+ hours)

All modules + all bonus content
*Focus: Complete expertise*

### Path D: Security Specialist (8 hours)

Modules 4, 7, 11 + security portions of other modules
*Focus: API security*

### Path E: DevOps Engineer (8 hours)

Modules 8, 12, 13 + deployment topics
*Focus: Operations and deployment*

---

# Assessment & Certification

## Module Quizzes

- 10 questions per module
- 70% passing score
- Unlimited retries

## Hands-On Projects

- Module 3: Build a complete endpoint
- Module 6: Implement caching layer
- Module 10: Advanced repository
- Module 13: Cloud deployment

## Final Capstone Project

- Build a complete modular API
- Implement authentication
- Add caching and validation
- Deploy to cloud
- Documentation required

## Certification Levels

- **Associate:** Complete Modules 1-9 + quizzes
- **Professional:** Complete all 14 modules + capstone
- **Expert:** Professional + all bonus modules + portfolio review

---

# Video Production Specifications

## Technical Requirements

| Aspect     | Specification                             |
|------------|-------------------------------------------|
| Resolution | 1920x1080 (1080p) minimum                 |
| Frame Rate | 30 fps                                    |
| Audio      | 48kHz, -16 LUFS                           |
| Code Font  | 16pt minimum, JetBrains Mono or Fira Code |
| IDE Theme  | Dark theme, high contrast                 |

## Video Types

| Type             | Format                  | Avg Duration |
|------------------|-------------------------|--------------|
| Lecture          | Slides + talking head   | 8-12 min     |
| Code Walkthrough | Screen + voiceover      | 10-15 min    |
| Code-Along       | Screen + typing + audio | 10-15 min    |
| Demo             | Screen recording        | 8-12 min     |
| Exercise         | Instructions + solution | 6-10 min     |

## Accessibility

- Closed captions for all videos
- Downloadable transcripts
- Screen reader friendly resources
- Keyboard navigation in exercises

---

# Platform & Pricing

## Platform Compatibility

- Udemy, Teachable, Thinkific
- Pluralsight, LinkedIn Learning
- Self-hosted LMS (SCORM compliant)

## Pricing Tiers

| Tier         | Includes                          | Suggested Price |
|--------------|-----------------------------------|-----------------|
| Basic        | Videos only                       | $99             |
| Standard     | Videos + resources + community    | $199            |
| Professional | Standard + live Q&A + code review | $399            |
| Team (5+)    | Professional + private Slack      | $299/seat       |

## Bundling Options

- Core course (Modules 1-9): $79
- Enterprise bundle (Modules 10-14): $79
- Bonus modules: $49 each or $149 all
- Complete bundle: $299 (save $100)

---

# Production Timeline

## Phase 1: Core Content (Weeks 1-8)

- Modules 1-9 recording and editing
- Resource creation
- Quiz development

## Phase 2: Enterprise Content (Weeks 9-14)

- Modules 10-14 recording and editing
- Case study development
- Capstone project design

## Phase 3: Bonus & Polish (Weeks 15-18)

- Bonus modules
- Platform setup
- Marketing materials
- Beta testing

## Phase 4: Launch (Week 19+)

- Soft launch to beta users
- Feedback incorporation
- Full public launch

---

*Course outline created: February 2026*
*Version: 2.0 - Expanded Edition*
*Based on: ASP.NET Core 10 Minimal APIs Workshop*
*Total Content: ~20 hours (113 main videos + 24 bonus videos)*
