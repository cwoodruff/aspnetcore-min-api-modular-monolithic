# Build Production-Ready APIs with ASP.NET Core

## Modular Monolithic Architecture Workshop

**A Full-Day, Hands-On Workshop for .NET Developers**

---

### Master Modern API Architecture Without the Microservices Complexity

Learn to build scalable, maintainable APIs using ASP.NET Core Minimal APIs and
the Modular Monolithic pattern. This architecture gives you clean module
boundaries and the flexibility to evolve toward microservices—without the
operational overhead from day one.

In this intensive workshop, you'll go beyond tutorials and build a real-world
API with authentication, caching, validation, and testing—all following
production-ready patterns used by teams at companies like Shopify, Basecamp, and
GitHub.

---

## What You'll Build

By the end of this workshop, you'll have built a complete modular API featuring:

- **5 Independent Modules** — Music, Orders, Administration, Reporting, and
  Identity
- **JWT Authentication** — Secure token issuance with RS256 signing and refresh
  rotation
- **Policy-Based Authorization** — Fine-grained permissions per module and
  tenant
- **Multi-Tier Caching** — Cache-aside pattern with tag-based invalidation
- **Input Validation** — FluentValidation with proper error responses
- **Rate Limiting** — Protect your API from abuse
- **Integration Tests** — Comprehensive testing with WebApplicationFactory

---

## What You'll Learn

### Architecture & Design

- When to choose Modular Monolith over Microservices (and vice versa)
- The IModule contract pattern for clean module composition
- Keeping modules independent while sharing infrastructure
- Preparing for future extraction to microservices

### Authentication & Security

- Implementing JWT Bearer authentication from scratch
- RS256 signing with JWKS endpoint for key distribution
- Building policy-based authorization with custom handlers
- Multi-tenant authorization patterns
- Rate limiting strategies for API protection

### Data Access & Performance

- Repository pattern with Entity Framework Core
- DbContext pooling for high throughput
- Service layer patterns that encapsulate business logic
- Cache-aside pattern with structured cache keys
- Tag-based cache invalidation on writes

### Code Quality & Testing

- FluentValidation for clean, testable validation
- RFC 7807 Problem Details for error responses
- Integration testing with WebApplicationFactory
- Testing authenticated endpoints

---

## Who Should Attend

This workshop is designed for:

- **.NET Developers** looking to level up their API architecture skills
- **Tech Leads** evaluating architecture patterns for new projects
- **Backend Engineers** transitioning from traditional MVC to Minimal APIs
- **Teams** considering microservices but wanting a pragmatic starting point

### Prerequisites

- Intermediate C# knowledge (classes, interfaces, async/await, LINQ)
- Basic understanding of HTTP and REST APIs
- Familiarity with ASP.NET Core (any version)
- Experience with Entity Framework Core is helpful but not required

### Technical Requirements

- Laptop with .NET 10+ SDK installed
- IDE: Visual Studio 2022, JetBrains Rider, or VS Code with C# Dev Kit
- Git installed
- Internet access (or we'll provide offline package cache)

---

## Workshop Format

**Duration:** Full Day (8:00 AM - 5:00 PM)

**Format:** Hands-on coding with instructor guidance

**Approach:** You'll start with a prepared base solution and progressively build
features. This lets you focus on learning patterns rather than typing
boilerplate.

### Schedule Overview

| Time         | Topic                                    |
|--------------|------------------------------------------|
| **8:00 AM**  | Welcome & Setup                          |
| **8:30 AM**  | Architecture Deep-Dive & Module Contract |
| **9:30 AM**  | Building Your First Module               |
| **10:30 AM** | *Break*                                  |
| **10:45 AM** | Authentication & Authorization           |
| **12:00 PM** | *Lunch*                                  |
| **12:45 PM** | Repository Pattern & Data Access         |
| **1:45 PM**  | Service Layer with Caching               |
| **2:45 PM**  | *Break*                                  |
| **3:00 PM**  | FluentValidation                         |
| **3:45 PM**  | Rate Limiting & Security                 |
| **4:30 PM**  | Testing & Wrap-Up                        |
| **5:00 PM**  | End                                      |

---

## What's Included

- **Starter Solution** — Pre-built foundation so you can focus on learning
- **Complete Source Code** — Full reference implementation to take home
- **Quick Reference Card** — Handy patterns and commands cheat sheet
- **Documentation** — Detailed architectural docs for your team
- **Certificate of Completion** — For professional development records

---

## Why Modular Monolith?

> *"Start with a modular monolith, evolve to microservices if and when you need
to."*
> — Martin Fowler

The Modular Monolithic architecture offers the best of both worlds:

| Traditional Monolith | Modular Monolith  | Microservices       |
|----------------------|-------------------|---------------------|
| Fast to start        | Fast to start     | Slower to start     |
| No boundaries        | Clear boundaries  | Strong boundaries   |
| Hard to scale teams  | Scales with teams | Scales with teams   |
| Simple deployment    | Simple deployment | Complex deployment  |
| Hard to evolve       | Easy to evolve    | Maximum flexibility |

**Perfect for:**

- Teams of 3-20 developers
- Projects with evolving requirements
- Organizations not ready for microservices operational overhead
- APIs that may need to extract services later

---

## Technologies Covered

- **ASP.NET Core 10** — Latest Minimal APIs
- **Entity Framework Core** — With SQLite (patterns apply to any database)
- **JWT Bearer Authentication** — RS256 signing, refresh tokens
- **FluentValidation** — Clean validation logic
- **xUnit** — Integration testing
- **Swagger/OpenAPI** — API documentation

---

## Sample Code Preview

Here's a taste of what you'll build:

### Module Contract

```csharp
public interface IModule
{
    string Name { get; }
    void RegisterServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

### Service with Caching

```csharp
public async Task<AlbumModel?> GetByIdAsync(int id, CancellationToken ct)
{
    var key = _keys.Compose("music", "album", "v1", $"by-id:{id}");

    return await _cache.GetOrAddAsync(key, async _ =>
        await _repository.GetById(id),
        new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = ["music:album"]
        }, ct);
}
```

### Minimal API Endpoint

```csharp
group.MapGet("/albums/{id}", [Authorize] async (
    int id,
    IAlbumService service,
    CancellationToken ct) =>
{
    var album = await service.GetByIdAsync(id, ct);
    return album is not null ? Results.Ok(album) : Results.NotFound();
})
.RequireAuthorization("music.read")
.WithName("GetAlbumById")
.Produces<AlbumModel>(200)
.Produces(404);
```

---

## Testimonials

> *"Finally, an architecture workshop that's practical! I was able to apply
these patterns to our project the very next week."*
> — Senior Developer, Enterprise Software Company

> *"The modular monolith approach was exactly what our team needed. We got clean
boundaries without the microservices tax."*
> — Tech Lead, SaaS Startup

> *"Best .NET workshop I've attended. The hands-on approach made everything
click."*
> — Backend Engineer, Financial Services

---

## Frequently Asked Questions

**Q: Is this workshop suitable for beginners?**
A: This workshop is designed for intermediate developers. You should be
comfortable with C# and have some ASP.NET Core experience. We won't cover C#
basics.

**Q: Do I need to bring my own laptop?**
A: Yes, please bring a laptop with the prerequisites installed. We'll send setup
instructions before the workshop.

**Q: Will I get the source code?**
A: Absolutely! You'll receive the complete starter solution, reference
implementation, and all documentation.

**Q: Can my team attend together?**
A: Yes! Team attendance is encouraged. Contact us for group pricing.

**Q: Is lunch provided?**
A: [Customize based on your event]

**Q: Can this workshop be delivered on-site for my company?**
A: Yes, we offer private workshops. Contact us for corporate training options.

---

## Ready to Level Up Your API Skills?

### Public Workshop

**Date:** [DATE]
**Time:** 8:00 AM - 5:00 PM
**Location:** [VENUE/VIRTUAL]
**Price:** [PRICE]

[**Register Now →**](#)

*Early bird pricing available until [DATE]*

---

### Private Team Training

Bring this workshop to your organization with customized content for your team's
needs.

- On-site or virtual delivery
- Customizable content and duration
- Team sizes from 5 to 30 developers
- Follow-up office hours available

[**Contact Us for Team Training →**](#)

---

## About the Instructor

**Chris Woody Woodruff**

Chris Woodruff has been at the forefront of software development since before the first .COM boom, building a career that spans enterprise web development, cloud solutions, software analytics, and developer relations. As an Architect, he applies his deep technical expertise to tackle complex challenges, with a particular focus on API design and scalable architectures. He is recognized as a Microsoft MVP specializing in .NET and Web Development. Woody’s impact extends beyond his professional responsibilities; he is a dedicated mentor and educator, teaching courses that help individuals transition into tech careers. His passion for sharing knowledge has made him a sought-after speaker at international conferences, where he discusses topics such as database development, web APIs, and software architecture. He contributes to the developer community by co-hosting **The Breakpoint Show** podcast and creating content that aids engineers in refining their skills. Previously, Woody led engineering teams at Rocket Homes, developed event-driven integration platforms, and spearheaded developer relations initiatives at Rocket Mortgage. His experience also includes serving as a Developer Advocate at JetBrains and architecting cloud-based analytics platforms at Eidex. Through his consulting work, he has assisted major companies, including Microsoft and MLB Advanced Media, in building robust software solutions. Beyond technology, Woody is an avid bourbon enthusiast, often exploring the Bourbon Trail in search of unique selections to share with friends. He also enjoys writing about his experiences in tech and life on his blog at https://woodruff.dev. You can stay connected with him on Bluesky at @woodruff.dev and on Mastodon at mastodon.social/@cwoodruff, where he engages with the developer community and shares insights on software, mentorship, and personal interests.

https://www.woodruff.dev/wp-content/uploads/2025/02/dne0BXsw9zrk-ZJDQGX7f.jpg

---

## Questions?

**Email:** cwoodruff@live.com
**Blusky:** @woodruff.dev
**LinkedIn:** /in/chriswoodruff/

---

*This workshop is based on a production-ready reference implementation available
on GitHub. Attendees receive full access to the codebase, documentation, and
ongoing updates.*
