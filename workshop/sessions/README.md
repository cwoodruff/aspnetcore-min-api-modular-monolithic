# Workshop Session Implementation Guides

## ASP.NET Core Minimal API: Modular Monolithic Architecture Workshop

This folder contains comprehensive implementation guides for each workshop session. Each guide includes the exact code from the solution repository.

---

## Session Guides

| Session | Title | Duration | Description |
|---------|-------|----------|-------------|
| [01](01-WELCOME-ENVIRONMENT-SETUP.md) | Welcome & Environment Setup | 30 min | Environment verification, solution setup |
| [02](02-ARCHITECTURE-MODULE-CONTRACT.md) | Architecture Overview & Module Contract | 60 min | Modular Monolith concepts, IModule pattern |
| [03](03-BUILDING-YOUR-FIRST-MODULE.md) | Building Your First Module | 60 min | Health endpoints, endpoint metadata |
| [04](04-AUTHENTICATION-AUTHORIZATION.md) | Authentication & Authorization | 75 min | JWT, policies, tenant scoping |
| [05](05-REPOSITORY-PATTERN-DATA-ACCESS.md) | Repository Pattern & Data Access | 60 min | Base repository, EF Core patterns |
| [06](06-SERVICE-LAYER-CACHING.md) | Service Layer with Caching | 60 min | Cache-aside pattern, tag invalidation |
| [07](07-FLUENT-VALIDATION.md) | FluentValidation | 45 min | Validators, problem details |
| [08](08-RATE-LIMITING-SECURITY.md) | Rate Limiting & Security | 45 min | Fixed window, partition keys |
| [09](09-TESTING-WRAP-UP.md) | Testing & Wrap-Up | 30 min | Integration tests, review |

---

## Daily Schedule

```
8:00 AM  - 8:30 AM   Session 1: Welcome & Environment Setup
8:30 AM  - 9:30 AM   Session 2: Architecture Overview & Module Contract
9:30 AM  - 10:30 AM  Session 3: Building Your First Module
10:30 AM - 10:45 AM  Morning Break
10:45 AM - 12:00 PM  Session 4: Authentication & Authorization
12:00 PM - 12:45 PM  Lunch Break
12:45 PM - 1:45 PM   Session 5: Repository Pattern & Data Access
1:45 PM  - 2:45 PM   Session 6: Service Layer with Caching
2:45 PM  - 3:00 PM   Afternoon Break
3:00 PM  - 3:45 PM   Session 7: FluentValidation
3:45 PM  - 4:30 PM   Session 8: Rate Limiting & Security
4:30 PM  - 5:00 PM   Session 9: Testing & Wrap-Up
```

---

## Guide Features

Each session guide includes:

- **Overview** - Session goals and context
- **Learning Objectives** - What you'll be able to do after completing
- **Exact Code** - Complete code samples from the solution repository
- **Explanations** - Why patterns are used
- **Checkpoints** - Verify your progress
- **Quick Reference** - Templates and cheat sheets

---

## Key Files by Session

### Session 1: Environment Setup
- No code files - environment verification

### Session 2: Architecture & Module Contract
- `src/Shared/SharedKernel/IModule.cs`
- `src/ModularMonolith.Api/Program.cs`
- All `src/Modules/*/Module.cs` files

### Session 3: Building Your First Module
- `src/Modules/Music/Music.Module/Endpoints/HealthEndpoints.cs`
- `src/Modules/Music/Music.Module/Endpoints/DataHealthEndpoints.cs`
- `src/Shared/SharedKernel/TrafficControl/RateLimitPolicyRegistry.cs`

### Session 4: Authentication & Authorization
- `src/Modules/Identity/Identity.Module/Extensions/IdentityAuthExtensions.cs`
- `src/Modules/Identity/Identity.Module/Services/TokenService.cs`
- `src/Modules/Identity/Identity.Module/Endpoints/AuthEndpoints.cs`
- `src/Modules/Identity/Identity.Module/Authorization/PolicyRegistry.cs`

### Session 5: Repository Pattern
- `src/Shared/SharedKernel.Persistence/Repositories/IRepository.cs`
- `src/Shared/SharedKernel.DataSQLite/Repositories/BaseRepository.cs`
- `src/Shared/SharedKernel.DataSQLite/Repositories/AlbumRepository.cs`
- `src/Shared/SharedKernel.Persistence/AppDbContext.cs`

### Session 6: Service Layer with Caching
- `src/Shared/SharedKernel/Caching/ICacheFacade.cs`
- `src/Shared/SharedKernel/Caching/CacheKeyComposer.cs`
- `src/Shared/SharedKernel/Caching/CompositeCacheFacade.cs`
- `src/Modules/Music/Music.Module/Services/AlbumService.cs`

### Session 7: FluentValidation
- `src/Shared/SharedKernel.Persistence/Validation/AlbumValidator.cs`
- `src/Shared/SharedKernel.Persistence/Validation/CustomerValidator.cs`
- `src/Shared/SharedKernel.Persistence/PersistenceRegistration.cs`

### Session 8: Rate Limiting & Security
- `src/Shared/SharedKernel/TrafficControl/RateLimitPolicyRegistry.cs`
- `src/Shared/SharedKernel/TrafficControl/PartitionKeys.cs`
- `src/ModularMonolith.Api/Program.cs` (rate limiting section)

### Session 9: Testing
- `tests/ModularMonolith.Api.Tests/TestAuthHelpers.cs`
- `tests/ModularMonolith.Api.Tests/AlbumEndpointsTests.cs`
- `tests/ModularMonolith.Api.Tests/HealthEndpointsTests.cs`

---

## Prerequisites

- .NET 10 SDK
- Git
- IDE (Rider, Visual Studio 2022, or VS Code)
- Basic C# knowledge
- Familiarity with ASP.NET Core

---

## Related Resources

- [Workshop Curriculum](../WORKSHOP-CURRICULUM.md) - Full workshop outline
- [Quick Reference](../QUICK-REFERENCE.md) - Cheat sheets
- [Starter Solution Guide](../STARTER-SOLUTION-GUIDE.md) - Setup instructions
- [Advanced Topics](../advanced-topics/) - Extension topics

---

## Code Verification

All code in these guides is extracted directly from the solution repository. To verify:

```bash
# Clone and build
git clone <repo-url>
cd aspnetcore-min-api-modular-monolithic
dotnet build

# Run tests
dotnet test

# Run the API
dotnet run --project src/ModularMonolith.Api
```

---

## Support

If you encounter issues during the workshop:

1. Check the troubleshooting sections in each guide
2. Review the complete solution in the main branch
3. Ask the instructor for assistance
