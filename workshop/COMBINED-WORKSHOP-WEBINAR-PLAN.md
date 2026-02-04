# ASP.NET Core Minimal APIs: Complete Training Plan

## Overview

This plan combines the **9 core workshop sessions** with **14 advanced topics**
into two delivery formats:

1. **In-Person Workshop** - 2-day intensive training
2. **Web Webinar Series** - 8-part weekly series

Both formats cover the complete curriculum for building production-ready APIs
using ASP.NET Core 10 Minimal APIs with Modular Monolithic Architecture.

---

## Content Inventory

### Core Sessions (8 hours total)

| Session | Topic                                   | Duration |
|---------|-----------------------------------------|----------|
| 01      | Welcome & Environment Setup             | 30 min   |
| 02      | Architecture Overview & Module Contract | 60 min   |
| 03      | Building Your First Module              | 60 min   |
| 04      | Authentication & Authorization          | 75 min   |
| 05      | Repository Pattern & Data Access        | 60 min   |
| 06      | Service Layer with Caching              | 60 min   |
| 07      | FluentValidation                        | 45 min   |
| 08      | Rate Limiting & Security                | 45 min   |
| 09      | Testing & Wrap-Up                       | 30 min   |

### Advanced Topics (10 hours total)

| Topic                | Category              | Duration |
|----------------------|-----------------------|----------|
| Endpoint Filters     | Core Extension        | 45 min   |
| Typed Results        | Quick Start           | 30 min   |
| Output Caching       | Core Extension        | 30 min   |
| Health Checks        | Quick Start           | 30 min   |
| Structured Logging   | Core Extension        | 45 min   |
| API Versioning       | Core Extension        | 30 min   |
| Background Services  | Advanced Architecture | 45 min   |
| OpenTelemetry        | Advanced Architecture | 45 min   |
| Feature Flags        | Core Extension        | 30 min   |
| Response Compression | Quick Start           | 20 min   |
| CQRS with MediatR    | Advanced Architecture | 60 min   |
| SignalR              | Integration           | 45 min   |
| gRPC                 | Integration           | 45 min   |
| Outbox Pattern       | Advanced Architecture | 60 min   |

---

# OPTION 1: In-Person Workshop (2 Days)

## Day 1: Foundations & Core Patterns

### Morning Block (8:00 AM - 12:00 PM)

| Time          | Duration | Content                                     | Type        |
|---------------|----------|---------------------------------------------|-------------|
| 8:00 - 8:30   | 30 min   | **Welcome & Environment Setup**             | Session 01  |
|               |          | - Prerequisites verification                |             |
|               |          | - Solution structure walkthrough            |             |
|               |          | - Chinook database overview                 |             |
| 8:30 - 9:30   | 60 min   | **Architecture Overview & Module Contract** | Session 02  |
|               |          | - Modular Monolith vs alternatives          |             |
|               |          | - IModule contract deep-dive                |             |
|               |          | - Host composition flow                     |             |
| 9:30 - 10:30  | 60 min   | **Building Your First Module**              | Session 03  |
|               |          | - Health endpoints                          |             |
|               |          | - Endpoint metadata                         |             |
|               |          | - Extension method patterns                 |             |
| 10:30 - 10:45 | 15 min   | **Break**                                   |             |
| 10:45 - 11:15 | 30 min   | **Typed Results**                           | Advanced 02 |
|               |          | - Better OpenAPI documentation              |             |
|               |          | - Compile-time safety                       |             |
| 11:15 - 12:00 | 45 min   | **Endpoint Filters**                        | Advanced 01 |
|               |          | - Cross-cutting concerns                    |             |
|               |          | - Validation filters                        |             |
|               |          | - Logging filters                           |             |

### Lunch Break (12:00 PM - 1:00 PM)

### Afternoon Block (1:00 PM - 5:30 PM)

| Time        | Duration | Content                              | Type        |
|-------------|----------|--------------------------------------|-------------|
| 1:00 - 2:15 | 75 min   | **Authentication & Authorization**   | Session 04  |
|             |          | - JWT Bearer authentication          |             |
|             |          | - RS256 signing & JWKS               |             |
|             |          | - Policy-based authorization         |             |
|             |          | - Multi-tenant patterns              |             |
| 2:15 - 3:15 | 60 min   | **Repository Pattern & Data Access** | Session 05  |
|             |          | - BaseRepository with CRUD           |             |
|             |          | - Entity-specific repositories       |             |
|             |          | - EF Core patterns                   |             |
|             |          | - DbContext pooling                  |             |
| 3:15 - 3:30 | 15 min   | **Break**                            |             |
| 3:30 - 4:30 | 60 min   | **Service Layer with Caching**       | Session 06  |
|             |          | - Cache-aside pattern                |             |
|             |          | - ICacheFacade & CacheKeyComposer    |             |
|             |          | - Tag-based invalidation             |             |
| 4:30 - 5:00 | 30 min   | **Output Caching**                   | Advanced 03 |
|             |          | - HTTP-level caching                 |             |
|             |          | - Complementing service caching      |             |
| 5:00 - 5:30 | 30 min   | **Day 1 Q&A & Preview**              |             |
|             |          | - Questions and discussion           |             |
|             |          | - Day 2 preview                      |             |

### Day 1 Summary

- **Total Instruction Time:** 7.5 hours
- **Hands-On Labs:** 4-5 exercises
- **Modules Built:** Reporting (health), partial Music module

---

## Day 2: Production Readiness & Advanced Patterns

### Morning Block (8:00 AM - 12:00 PM)

| Time          | Duration | Content                       | Type        |
|---------------|----------|-------------------------------|-------------|
| 8:00 - 8:15   | 15 min   | **Day 1 Recap & Questions**   |             |
| 8:15 - 9:00   | 45 min   | **FluentValidation**          | Session 07  |
|               |          | - Validator creation          |             |
|               |          | - Service integration         |             |
|               |          | - ProblemDetails responses    |             |
| 9:00 - 9:45   | 45 min   | **Rate Limiting & Security**  | Session 08  |
|               |          | - Rate limiting policies      |             |
|               |          | - Security headers            |             |
|               |          | - CORS configuration          |             |
| 9:45 - 10:00  | 15 min   | **Break**                     |             |
| 10:00 - 10:30 | 30 min   | **Health Checks**             | Advanced 04 |
|               |          | - Production health endpoints |             |
|               |          | - Dependency health checks    |             |
| 10:30 - 11:15 | 45 min   | **Structured Logging**        | Advanced 05 |
|               |          | - Serilog integration         |             |
|               |          | - Correlation IDs             |             |
|               |          | - Centralized logging         |             |
| 11:15 - 12:00 | 45 min   | **OpenTelemetry**             | Advanced 08 |
|               |          | - Distributed tracing         |             |
|               |          | - Metrics collection          |             |
|               |          | - Observability patterns      |             |

### Lunch Break (12:00 PM - 1:00 PM)

### Afternoon Block (1:00 PM - 5:00 PM)

| Time        | Duration | Content                                    | Type        |
|-------------|----------|--------------------------------------------|-------------|
| 1:00 - 1:30 | 30 min   | **API Versioning**                         | Advanced 06 |
|             |          | - Version strategies                       |             |
|             |          | - Backward compatibility                   |             |
| 1:30 - 2:00 | 30 min   | **Feature Flags**                          | Advanced 09 |
|             |          | - Toggle features                          |             |
|             |          | - Gradual rollouts                         |             |
| 2:00 - 2:45 | 45 min   | **Background Services**                    | Advanced 07 |
|             |          | - Async processing                         |             |
|             |          | - Scheduled tasks                          |             |
|             |          | - Queue patterns                           |             |
| 2:45 - 3:00 | 15 min   | **Break**                                  |             |
| 3:00 - 3:20 | 20 min   | **Response Compression**                   | Advanced 10 |
|             |          | - HTTP compression                         |             |
|             |          | - Performance gains                        |             |
| 3:20 - 4:05 | 45 min   | **Choose Your Path** (pick one)            |             |
|             |          | Option A: **SignalR** - Real-time features | Advanced 12 |
|             |          | Option B: **gRPC** - Service-to-service    | Advanced 13 |
| 4:05 - 4:35 | 30 min   | **Testing & Integration**                  | Session 09  |
|             |          | - WebApplicationFactory                    |             |
|             |          | - Authenticated endpoint tests             |             |
| 4:35 - 5:00 | 25 min   | **Wrap-Up & Next Steps**                   |             |
|             |          | - Key takeaways                            |             |
|             |          | - Self-study: CQRS/MediatR, Outbox Pattern |             |
|             |          | - Resources and Q&A                        |             |

### Day 2 Summary

- **Total Instruction Time:** 7.5 hours
- **Hands-On Labs:** 5-6 exercises
- **Topics for Self-Study:** CQRS/MediatR (60 min), Outbox Pattern (60 min)

---

## In-Person Workshop Materials Checklist

### Pre-Workshop

- [ ] Participant prerequisites email (1 week before)
- [ ] Environment setup guide
- [ ] Repository access instructions
- [ ] Docker images pre-built (optional)

### During Workshop

- [ ] Slides for each session
- [ ] Printed quick reference cards
- [ ] USB drives with offline content
- [ ] WiFi network details

### Post-Workshop

- [ ] Certificate of completion
- [ ] Access to advanced topics (CQRS, Outbox)
- [ ] Community Slack/Discord invite
- [ ] Recording access (if recorded)

---

# OPTION 2: Web Webinar Series (8 Weeks)

## Series Overview

**Format:** 8 weekly webinars, 90-120 minutes each
**Delivery:** Live with Q&A, recordings available
**Labs:** Homework assignments between sessions

---

## Webinar 1: Foundations & Architecture

**Duration:** 120 minutes | **Week 1**

| Segment    | Duration | Content                                            |
|------------|----------|----------------------------------------------------|
| Welcome    | 15 min   | Course overview, prerequisites, setup verification |
| Session 01 | 20 min   | Environment Setup & Solution Structure             |
| Session 02 | 45 min   | Architecture Overview & Module Contract            |
| Break      | 5 min    |                                                    |
| Hands-On   | 25 min   | Explore the solution, run the API                  |
| Q&A        | 10 min   | Questions and discussion                           |

**Homework:** Complete environment setup, explore Chinook database

---

## Webinar 2: Building Modules & Endpoints

**Duration:** 120 minutes | **Week 2**

| Segment     | Duration | Content                                    |
|-------------|----------|--------------------------------------------|
| Recap       | 10 min   | Week 1 review, homework discussion         |
| Session 03  | 50 min   | Building Your First Module                 |
| Advanced 02 | 25 min   | Typed Results                              |
| Break       | 5 min    |                                            |
| Hands-On    | 20 min   | Create health endpoints, add typed results |
| Q&A         | 10 min   | Questions and discussion                   |

**Homework:** Build a new endpoint with typed results

---

## Webinar 3: Authentication & Authorization

**Duration:** 120 minutes | **Week 3**

| Segment    | Duration | Content                                |
|------------|----------|----------------------------------------|
| Recap      | 10 min   | Week 2 review                          |
| Session 04 | 70 min   | Authentication & Authorization         |
| Break      | 5 min    |                                        |
| Hands-On   | 25 min   | Implement JWT login, protect endpoints |
| Q&A        | 10 min   | Questions and discussion               |

**Homework:** Add custom authorization policy

---

## Webinar 4: Data Access & Repository Pattern

**Duration:** 120 minutes | **Week 4**

| Segment     | Duration | Content                          |
|-------------|----------|----------------------------------|
| Recap       | 10 min   | Week 3 review                    |
| Session 05  | 55 min   | Repository Pattern & Data Access |
| Advanced 01 | 35 min   | Endpoint Filters                 |
| Break       | 5 min    |                                  |
| Hands-On    | 15 min   | Implement a new repository       |
| Q&A         | 10 min   | Questions and discussion         |

**Homework:** Create entity-specific repository with filters

---

## Webinar 5: Caching & Performance

**Duration:** 120 minutes | **Week 5**

| Segment     | Duration | Content                       |
|-------------|----------|-------------------------------|
| Recap       | 10 min   | Week 4 review                 |
| Session 06  | 50 min   | Service Layer with Caching    |
| Advanced 03 | 25 min   | Output Caching                |
| Advanced 10 | 15 min   | Response Compression          |
| Break       | 5 min    |                               |
| Hands-On    | 15 min   | Implement cache-aside pattern |
| Q&A         | 10 min   | Questions and discussion      |

**Homework:** Add caching to existing service

---

## Webinar 6: Validation, Rate Limiting & Security

**Duration:** 120 minutes | **Week 6**

| Segment    | Duration | Content                          |
|------------|----------|----------------------------------|
| Recap      | 10 min   | Week 5 review                    |
| Session 07 | 40 min   | FluentValidation                 |
| Session 08 | 40 min   | Rate Limiting & Security         |
| Break      | 5 min    |                                  |
| Hands-On   | 15 min   | Add validators and rate limiting |
| Q&A        | 10 min   | Questions and discussion         |

**Homework:** Create custom validators with complex rules

---

## Webinar 7: Production Readiness

**Duration:** 120 minutes | **Week 7**

| Segment     | Duration | Content                             |
|-------------|----------|-------------------------------------|
| Recap       | 10 min   | Week 6 review                       |
| Advanced 04 | 25 min   | Health Checks                       |
| Advanced 05 | 35 min   | Structured Logging                  |
| Advanced 08 | 35 min   | OpenTelemetry                       |
| Break       | 5 min    |                                     |
| Demo        | 10 min   | Observability dashboard walkthrough |
| Q&A         | 10 min   | Questions and discussion            |

**Homework:** Configure structured logging and health checks

---

## Webinar 8: Advanced Patterns & Wrap-Up

**Duration:** 120 minutes | **Week 8**

| Segment     | Duration | Content                              |
|-------------|----------|--------------------------------------|
| Recap       | 10 min   | Week 7 review                        |
| Advanced 06 | 20 min   | API Versioning                       |
| Advanced 09 | 20 min   | Feature Flags                        |
| Advanced 07 | 30 min   | Background Services                  |
| Break       | 5 min    |                                      |
| Session 09  | 20 min   | Testing & Integration                |
| Wrap-Up     | 15 min   | Key takeaways, next steps, resources |

**Self-Study Resources:** CQRS/MediatR, SignalR, gRPC, Outbox Pattern

---

## Webinar Series Materials

### Platform Requirements

- Streaming: Zoom, Teams, or YouTube Live
- Chat: Real-time Q&A during sessions
- Repository: GitHub with branch per week
- Community: Discord or Slack for async support

### Per-Webinar Deliverables

- [ ] Slide deck (PDF)
- [ ] Session recording
- [ ] Code samples (GitHub branch)
- [ ] Homework assignment
- [ ] Quick reference sheet

### Series Completion

- [ ] Certificate of completion
- [ ] Full repository access
- [ ] Bonus: Advanced topics guides (PDF)
- [ ] Community membership

---

# Comparison: In-Person vs Webinar

| Aspect          | In-Person (2 Days)        | Webinar (8 Weeks)                    |
|-----------------|---------------------------|--------------------------------------|
| **Duration**    | 15 hours over 2 days      | 16 hours over 8 weeks                |
| **Pacing**      | Intensive                 | Gradual with practice time           |
| **Interaction** | High, immediate feedback  | Moderate, chat-based Q&A             |
| **Hands-On**    | Supervised labs           | Homework assignments                 |
| **Networking**  | Strong peer interaction   | Online community                     |
| **Best For**    | Teams, conferences        | Individual learners, global audience |
| **Cost Model**  | Higher per-seat, one-time | Lower, subscription possible         |

---

# Content Coverage Matrix

| Topic                          | In-Person Day      | Webinar Week |
|--------------------------------|--------------------|--------------|
| Environment Setup              | Day 1              | Week 1       |
| Architecture & Module Contract | Day 1              | Week 1       |
| Building Modules               | Day 1              | Week 2       |
| Typed Results                  | Day 1              | Week 2       |
| Endpoint Filters               | Day 1              | Week 4       |
| Authentication & Authorization | Day 1              | Week 3       |
| Repository Pattern             | Day 1              | Week 4       |
| Service Layer & Caching        | Day 1              | Week 5       |
| Output Caching                 | Day 1              | Week 5       |
| FluentValidation               | Day 2              | Week 6       |
| Rate Limiting & Security       | Day 2              | Week 6       |
| Health Checks                  | Day 2              | Week 7       |
| Structured Logging             | Day 2              | Week 7       |
| OpenTelemetry                  | Day 2              | Week 7       |
| API Versioning                 | Day 2              | Week 8       |
| Feature Flags                  | Day 2              | Week 8       |
| Background Services            | Day 2              | Week 8       |
| Response Compression           | Day 2              | Week 5       |
| SignalR/gRPC                   | Day 2 (choose one) | Self-study   |
| CQRS/MediatR                   | Self-study         | Self-study   |
| Outbox Pattern                 | Self-study         | Self-study   |
| Testing                        | Day 2              | Week 8       |

---

# Marketing Taglines

## In-Person Workshop

> **"Master ASP.NET Core Minimal APIs in 2 Days"**
> Intensive, hands-on training with expert guidance. Build production-ready
> modular monoliths.

## Webinar Series

> **"8 Weeks to Production-Ready APIs"**
> Learn at your own pace with weekly live sessions, homework, and community
> support.

---

# Instructor Notes

## Key Teaching Points

1. **Module Contract** - The heart of the architecture; spend time here
2. **Cache-Aside Pattern** - Common production pattern, ensure hands-on practice
3. **JWT + Policies** - Security is critical; cover edge cases
4. **Repository vs Direct DbContext** - Explain when to use each
5. **Testing** - Don't skip; essential for production code

## Common Questions to Prepare For

- When to use Modular Monolith vs Microservices?
- How to handle cross-module communication?
- Database per module vs shared database?
- How to migrate from traditional controllers?
- Performance comparison: Minimal APIs vs Controllers?

## Lab Environment Tips

- Pre-test all labs on Windows, Mac, and Linux
- Have fallback Docker containers ready
- Prepare offline SQLite database copies
- Test with .NET 10 preview and stable versions

---

# Next Steps

1. **Select Format** - Choose in-person, webinar, or both
2. **Set Dates** - Schedule sessions and announce
3. **Prepare Materials** - Finalize slides, labs, and recordings
4. **Marketing** - Use landing page, email templates, and flyer
5. **Platform Setup** - Configure streaming, GitHub, and community
6. **Dry Run** - Test full environment before launch

---

*Plan created: February 2026*
*Based on: ASP.NET Core 10 Minimal APIs Modular Monolith Workshop*
