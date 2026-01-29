# ASP.NET Core Minimal API: Modular Monolithic Architecture Workshop

**Duration:** Full Day (8:00 AM - 5:00 PM)
**Level:** Intermediate to Advanced
**Prerequisites:** Basic C#, familiarity with ASP.NET Core, understanding of
REST APIs

---

## Workshop Overview

In this hands-on workshop, participants will learn to build a production-ready
ASP.NET Core 10 Minimal API using Modular Monolithic architecture. Starting from
a prepared base solution, attendees will implement module composition,
authentication, caching, validation, and testing patterns.

### Learning Objectives

By the end of this workshop, participants will be able to:

1. Understand and implement the Modular Monolithic architecture pattern
2. Create and compose modules using the IModule contract
3. Implement JWT authentication and policy-based authorization
4. Build a service layer with caching and validation
5. Apply FluentValidation for input validation
6. Implement rate limiting for API protection
7. Write integration tests using WebApplicationFactory

---

## Schedule at a Glance

| Time                | Duration | Session                                               |
|---------------------|----------|-------------------------------------------------------|
| 8:00 AM - 8:30 AM   | 30 min   | **Welcome & Environment Setup**                       |
| 8:30 AM - 9:30 AM   | 60 min   | **Module 1: Architecture Overview & Module Contract** |
| 9:30 AM - 10:30 AM  | 60 min   | **Module 2: Building Your First Module**              |
| 10:30 AM - 10:45 AM | 15 min   | **☕ Morning Break**                                   |
| 10:45 AM - 12:00 PM | 75 min   | **Module 3: Authentication & Authorization**          |
| 12:00 PM - 12:45 PM | 45 min   | **🍽️ Lunch Break**                                   |
| 12:45 PM - 1:45 PM  | 60 min   | **Module 4: Repository Pattern & Data Access**        |
| 1:45 PM - 2:45 PM   | 60 min   | **Module 5: Service Layer with Caching**              |
| 2:45 PM - 3:00 PM   | 15 min   | **☕ Afternoon Break**                                 |
| 3:00 PM - 3:45 PM   | 45 min   | **Module 6: FluentValidation**                        |
| 3:45 PM - 4:30 PM   | 45 min   | **Module 7: Rate Limiting & Security**                |
| 4:30 PM - 5:00 PM   | 30 min   | **Module 8: Testing & Wrap-Up**                       |

---

## Base Solution (Pre-Built)

Participants start with a prepared solution containing the foundational
infrastructure. This allows focus on architectural patterns rather than
boilerplate setup.

### What's Included in the Base Solution

```
/workshop-starter
  /src
    /ModularMonolith.Api
      - Program.cs (minimal - just WebApplication setup)
      - appsettings.json (database connection configured)
      - data/chinook.db (SQLite database with sample data)
    /Modules
      /Music/Music.Module             (empty class library)
      /Orders/Orders.Module           (empty class library)
      /Administration/Admin.Module    (empty class library)
      /Reporting/Reporting.Module     (empty class library)
      /Identity/Identity.Module       (empty class library)
    /Shared
      /SharedKernel
        - IModule.cs (interface defined)
      /SharedKernel.Persistence
        - Entities/ (all Chinook entities)
        - ApiModels/ (all DTOs)
        - AppDbContext.cs (configured)
        - IAppDbContext.cs
        - PersistenceRegistration.cs
        - Repositories/ (interfaces only)
      /SharedKernel.DataSQLite        (empty - repositories to be built)
  /tests
    /ModularMonolith.Api.Tests        (basic test project setup)
  ModularMonolith.Api.sln
  Directory.Build.props
```

### What Participants Will Build

1. Repository implementations in SharedKernel.DataSQLite
2. Complete Identity module (auth endpoints, JWT, policies)
3. Music module with service layer, caching, validation
4. Administration module with CRUD operations
5. Rate limiting configuration
6. Integration tests

---

## Detailed Session Breakdown

---

## Session 1: Welcome & Environment Setup (8:00 AM - 8:30 AM)

**Duration:** 30 minutes

### Objectives

- Verify development environment
- Clone and open the starter solution
- Understand the workshop structure

### Activities

#### 1.1 Environment Check (10 min)

Verify installations:

```bash
dotnet --version    # Should be 9.0+ or 10.0
git --version
```

IDE options:

- JetBrains Rider (recommended)
- Visual Studio 2022
- VS Code with C# Dev Kit

#### 1.2 Clone Starter Solution (5 min)

```bash
git clone <workshop-repo-url> workshop-starter
cd workshop-starter
dotnet restore
dotnet build
```

#### 1.3 Explore the Base Solution (15 min)

- Review solution structure
- Examine the Chinook database schema
- Review entity models and DTOs
- Understand the IModule contract

### Instructor Notes

- Ensure all participants can build the solution
- Have USB drives with offline copies for network issues
- Display the solution structure diagram

---

## Session 2: Architecture Overview & Module Contract (8:30 AM - 9:30 AM)

**Duration:** 60 minutes

### Objectives

- Understand Modular Monolithic architecture benefits
- Learn the IModule contract pattern
- Configure the host to discover and compose modules

### Concepts Covered

#### 2.1 Architecture Presentation (20 min)

**Modular Monolith vs Microservices vs Traditional Monolith**

| Aspect        | Traditional Monolith | Modular Monolith               | Microservices            |
|---------------|----------------------|--------------------------------|--------------------------|
| Deployment    | Single unit          | Single unit                    | Independent              |
| Boundaries    | None/weak            | Strong (compile-time)          | Strong (runtime)         |
| Communication | Direct calls         | Direct calls                   | Network (HTTP/messaging) |
| Data          | Shared DB            | Shared DB (logical separation) | Per-service DB           |
| Complexity    | Low                  | Medium                         | High                     |
| Team scaling  | Limited              | Good                           | Excellent                |

**When to choose Modular Monolith:**

- Starting a new project with uncertain boundaries
- Team size 3-15 developers
- Need rapid development with clean architecture
- Plan to potentially extract microservices later

#### 2.2 The IModule Contract (15 min)

Review the contract in SharedKernel:

```csharp
public interface IModule
{
    string Name { get; }
    void RegisterServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

**Key principles:**

- Modules are self-contained
- Only IModule is public; internals remain internal
- Modules don't reference each other directly
- Host orchestrates module composition

#### 2.3 Hands-On: Configure Host Composition (25 min)

**Exercise:** Update Program.cs to discover and compose modules

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Get all modules
var modules = GetModules();

// Register services from each module
foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

// Map endpoints from each module
foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

static IReadOnlyList<IModule> GetModules() =>
[
    // Modules will be added here as we build them
];
```

### Checkpoint

- [ ] Participants understand modular monolith benefits
- [ ] Program.cs configured for module composition
- [ ] Solution builds successfully

---

## Session 3: Building Your First Module (9:30 AM - 10:30 AM)

**Duration:** 60 minutes

### Objectives

- Create a complete module following the pattern
- Implement health endpoints
- Understand endpoint grouping and metadata

### Hands-On Exercises

#### 3.1 Create the Reporting Module (20 min)

The simplest module - health endpoints only.

**File: src/Modules/Reporting/Reporting.Module/Module.cs**

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Reporting.Modules;

public static class ReportingModule
{
    public sealed class Modules : IModule
    {
        public string Name => "Reporting";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            // No services for now
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/reporting");

            group.MapGet("/health", () => Results.Ok(new
            {
                module = "Reporting",
                status = "Healthy",
                timestampUtc = DateTime.UtcNow
            }))
            .WithName("ReportingHealth")
            .WithTags("Reporting")
            .Produces<object>(StatusCodes.Status200OK);
        }
    }
}
```

**Update Program.cs:**

```csharp
static IReadOnlyList<IModule> GetModules() =>
[
    new Reporting.Modules.ReportingModule.Modules()
];
```

**Test it:**

```bash
dotnet run --project src/ModularMonolith.Api
curl http://localhost:5043/api/reporting/health
```

#### 3.2 Add Data Health Endpoint (20 min)

Check database connectivity:

```csharp
group.MapGet("/data-health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    var genreCount = canConnect ? await db.Genres.CountAsync(ct) : 0;

    return Results.Ok(new
    {
        module = "Reporting",
        status = canConnect ? "Healthy" : "Unhealthy",
        database = canConnect ? "Connected" : "Disconnected",
        sampleCount = new { genres = genreCount },
        timestampUtc = DateTime.UtcNow
    });
})
.WithName("ReportingDataHealth")
.WithTags("Reporting");
```

#### 3.3 Create Music Module Health Endpoints (20 min)

**Exercise:** Create Music.Module following the same pattern

**File: src/Modules/Music/Music.Module/Module.cs**

Participants implement:

- Health endpoint at `/api/music/health`
- Data-health endpoint at `/api/music/data-health`
- Add module to GetModules() in Program.cs

### Checkpoint

- [ ] Reporting module working with health endpoints
- [ ] Music module created with health endpoints
- [ ] Both modules compose correctly in Program.cs

---

## ☕ Morning Break (10:30 AM - 10:45 AM)

---

## Session 4: Authentication & Authorization (10:45 AM - 12:00 PM)

**Duration:** 75 minutes

### Objectives

- Implement JWT bearer authentication
- Create login and token refresh endpoints
- Define authorization policies
- Protect endpoints with policies

### Concepts Covered

#### 4.1 JWT Authentication Overview (15 min)

**Token anatomy:**

- Header: algorithm, type, key ID
- Payload: claims (sub, name, roles, permissions, tenant, exp)
- Signature: RS256 signed

**Our token design:**

```json
{
  "sub": "user-1",
  "name": "Demo User",
  "email": "demo@example.com",
  "roles": ["User"],
  "permissions": ["music.read", "orders.read"],
  "tenant": "tenant-1",
  "exp": 1234567890
}
```

#### 4.2 Hands-On: Build the Identity Module (60 min)

**Step 1: Create Key Material Service (10 min)**

```csharp
// Identity.Module/KeyManagement/IKeyMaterialService.cs
public interface IKeyMaterialService
{
    RSA GetSigningKey();
    string GetKeyId();
    JsonWebKeySet GetJwks();
}

// Identity.Module/KeyManagement/DevKeyMaterialService.cs
public sealed class DevKeyMaterialService : IKeyMaterialService
{
    private readonly RSA _rsa;
    private readonly string _keyId;

    public DevKeyMaterialService()
    {
        _rsa = RSA.Create(2048);
        _keyId = Guid.NewGuid().ToString("N")[..8];
    }

    public RSA GetSigningKey() => _rsa;
    public string GetKeyId() => _keyId;

    public JsonWebKeySet GetJwks()
    {
        var parameters = _rsa.ExportParameters(false);
        var jwk = new JsonWebKey
        {
            Kty = "RSA",
            Use = "sig",
            Kid = _keyId,
            N = Base64UrlEncoder.Encode(parameters.Modulus),
            E = Base64UrlEncoder.Encode(parameters.Exponent)
        };
        return new JsonWebKeySet { Keys = { jwk } };
    }
}
```

**Step 2: Create Token Service (15 min)**

```csharp
// Identity.Module/Services/TokenService.cs
public sealed class TokenService
{
    private readonly IKeyMaterialService _keyService;
    private readonly JwtAuthOptions _options;

    public TokenService(IKeyMaterialService keyService, IOptions<JwtAuthOptions> options)
    {
        _keyService = keyService;
        _options = options.Value;
    }

    public TokenPair Issue(string userId, string displayName,
        string[] roles, string[] permissions, string email, string tenant)
    {
        var now = DateTime.UtcNow;
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshExpires = now.AddDays(_options.RefreshTokenDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.Email, email),
            new("tenant", tenant),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(r => new Claim("roles", r)));
        claims.AddRange(permissions.Select(p => new Claim("permissions", p)));

        var key = new RsaSecurityKey(_keyService.GetSigningKey())
        {
            KeyId = _keyService.GetKeyId()
        };
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        return new TokenPair(accessToken, refreshToken, accessExpires, refreshExpires);
    }
}
```

**Step 3: Create Demo User Store (10 min)**

```csharp
// Identity.Module/Services/InMemoryUserStore.cs
public sealed class InMemoryUserStore : IUserStore
{
    private static readonly Dictionary<string, DemoUser> Users = new()
    {
        ["demo"] = new("user-1", "Demo User", "demo@example.com", "demo123!",
            ["User"], ["music.read"], "tenant-1"),
        ["admin"] = new("user-2", "Admin User", "admin@example.com", "admin123!",
            ["Admin"], ["music.read", "music.write", "orders.read", "orders.write",
                       "administration.read", "administration.write"], "tenant-1")
    };

    public Task<DemoUser?> ValidateCredentialsAsync(string username, string password)
    {
        if (Users.TryGetValue(username, out var user) && user.Password == password)
            return Task.FromResult<DemoUser?>(user);
        return Task.FromResult<DemoUser?>(null);
    }
}
```

**Step 4: Create Auth Endpoints (15 min)**

```csharp
// Identity.Module/Endpoints/AuthEndpoints.cs
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapPost("/login", async (
            LoginRequest request,
            IUserStore userStore,
            TokenService tokenService) =>
        {
            var user = await userStore.ValidateCredentialsAsync(
                request.Username, request.Password);

            if (user is null)
                return Results.Unauthorized();

            var tokens = tokenService.Issue(
                user.Id, user.DisplayName, user.Roles,
                user.Permissions, user.Email, user.Tenant);

            return Results.Ok(new
            {
                access_token = tokens.AccessToken,
                refresh_token = tokens.RefreshToken,
                token_type = "Bearer",
                expires_at_utc = tokens.AccessTokenExpires
            });
        })
        .AllowAnonymous()
        .WithName("Login");

        group.MapGet("/.well-known/jwks.json", (IKeyMaterialService keyService) =>
            Results.Ok(keyService.GetJwks()))
        .AllowAnonymous()
        .WithName("JWKS");
    }
}
```

**Step 5: Register Authentication (10 min)**

```csharp
// Identity.Module/Extensions/IdentityAuthExtensions.cs
public static class IdentityAuthExtensions
{
    public static IServiceCollection AddIdentityAuth(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtAuthOptions>(config.GetSection("Jwt"));
        services.AddSingleton<IKeyMaterialService, DevKeyMaterialService>();
        services.AddSingleton<IUserStore, InMemoryUserStore>();
        services.AddScoped<TokenService>();

        var jwtOptions = config.GetSection("Jwt").Get<JwtAuthOptions>()!;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
                    {
                        var keyService = services.BuildServiceProvider()
                            .GetRequiredService<IKeyMaterialService>();
                        return [new RsaSecurityKey(keyService.GetSigningKey())
                        {
                            KeyId = keyService.GetKeyId()
                        }];
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("music.read", policy =>
                policy.RequireClaim("permissions", "music.read"));
            options.AddPolicy("music.write", policy =>
                policy.RequireClaim("permissions", "music.write"));
            // Add more policies...
        });

        return services;
    }
}
```

### Checkpoint

- [ ] Login endpoint returns JWT
- [ ] JWKS endpoint returns public key
- [ ] Protected endpoints require valid token

---

## 🍽️ Lunch Break (12:00 PM - 12:45 PM)

---

## Session 5: Repository Pattern & Data Access (12:45 PM - 1:45 PM)

**Duration:** 60 minutes

### Objectives

- Implement the repository pattern
- Create base repository with common operations
- Build entity-specific repositories with complex queries

### Hands-On Exercises

#### 5.1 Create Base Repository (20 min)

**File: SharedKernel.DataSQLite/Repositories/BaseRepository.cs**

```csharp
public abstract class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _context;

    protected BaseRepository(AppDbContext context)
    {
        _context = context;
    }

    public virtual async Task<bool> EntityExists(int id)
    {
        return await _context.Set<T>().AnyAsync(e => e.Id == id);
    }

    public virtual async Task<List<T>> GetAll()
    {
        return await _context.Set<T>().AsNoTracking().ToListAsync();
    }

    public virtual async Task<T> Add(T entity)
    {
        await _context.Set<T>().AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task<bool> Update(T entity)
    {
        if (!await EntityExists(entity.Id))
            return false;

        _context.Set<T>().Update(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public virtual async Task<bool> Delete(int id)
    {
        var entity = await _context.Set<T>().FindAsync(id);
        if (entity is null)
            return false;

        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
```

#### 5.2 Create Genre Repository (15 min)

Simple repository for the Genre entity:

```csharp
// SharedKernel.DataSQLite/Repositories/GenreRepository.cs
public sealed class GenreRepository : BaseRepository<Genre>, IGenreRepository
{
    public GenreRepository(AppDbContext context) : base(context) { }

    public async Task<GenreApiModel?> GetById(int id)
    {
        var entity = await _context.Genres
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id);

        return entity?.Convert();
    }
}
```

#### 5.3 Create Artist Repository with Complex Queries (25 min)

Repository with eager loading and projections:

```csharp
// SharedKernel.DataSQLite/Repositories/ArtistRepository.cs
public sealed class ArtistRepository : BaseRepository<Artist>, IArtistRepository
{
    public ArtistRepository(AppDbContext context) : base(context) { }

    public async Task<ArtistApiModel> GetById(int id)
    {
        // Load complete artist graph with split queries
        var entity = await _context.Artists
            .Where(a => a.Id == id)
            .Include(a => a.Albums)
                .ThenInclude(al => al.Tracks)
                    .ThenInclude(t => t.Genre)
            .Include(a => a.Albums)
                .ThenInclude(al => al.Tracks)
                    .ThenInclude(t => t.MediaType)
            .AsNoTracking()
            .AsSplitQuery()  // Important for SQLite performance
            .SingleAsync();

        // Project to DTO in memory
        return new ArtistApiModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Albums = entity.Albums.Select(al => new AlbumApiModel
            {
                Id = al.Id,
                Title = al.Title,
                Tracks = al.Tracks.Select(t => new TrackApiModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    GenreName = t.Genre?.Name,
                    MediaTypeName = t.MediaType?.Name
                }).ToList()
            }).ToList()
        };
    }
}
```

#### 5.4 Register Repositories (10 min)

**Update Program.cs:**

```csharp
// Register repositories
builder.Services
    .AddScoped<IGenreRepository, GenreRepository>()
    .AddScoped<IArtistRepository, ArtistRepository>()
    .AddScoped<IAlbumRepository, AlbumRepository>();
```

### Checkpoint

- [ ] Base repository provides CRUD operations
- [ ] Genre repository works with simple entity
- [ ] Artist repository handles complex relationships

---

## Session 6: Service Layer with Caching (1:45 PM - 2:45 PM)

**Duration:** 60 minutes

### Objectives

- Build service layer that wraps repositories
- Implement cache-aside pattern
- Apply tag-based cache invalidation

### Concepts Covered

#### 6.1 Cache Architecture Overview (10 min)

**Cache-aside pattern:**

1. Check cache for key
2. On miss, load from database
3. Store result in cache
4. Return data

**Our caching infrastructure:**

- `ICacheFacade` - Main abstraction
- `ICacheKeyComposer` - Structured key generation
- `CacheEntryOptions` - TTL and tags

**Cache key structure:**

```
{env}:{app}:{module}:{entity}:{version}:{discriminator}
Example: prod:mmapi:music:artist:v1:by-id:42
```

#### 6.2 Hands-On: Create Genre Service (25 min)

```csharp
// Admin.Module/Services/IGenreService.cs
public interface IGenreService
{
    Task<GenreApiModel?> GetGenreByIdAsync(int id, CancellationToken ct);
    Task<IEnumerable<GenreApiModel>> GetAllGenresAsync(CancellationToken ct);
    Task<GenreApiModel?> CreateGenreAsync(string name, CancellationToken ct);
    Task<bool> UpdateGenreAsync(int id, string name, CancellationToken ct);
    Task<bool> DeleteGenreAsync(int id, CancellationToken ct);
}

// Admin.Module/Services/GenreService.cs
public sealed class GenreService : IGenreService
{
    private readonly IGenreRepository _repo;
    private readonly ICacheFacade _cache;
    private readonly ICacheKeyComposer _keys;

    private static readonly string[] GenreTags =
        ["administration:genre", "administration:genre:by-id"];

    public GenreService(
        IGenreRepository repo,
        ICacheFacade cache,
        ICacheKeyComposer keys)
    {
        _repo = repo;
        _cache = cache;
        _keys = keys;
    }

    public async Task<GenreApiModel?> GetGenreByIdAsync(int id, CancellationToken ct)
    {
        var key = _keys.Compose(
            moduleName: "administration",
            entity: "genre",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await _cache.GetOrAddAsync<GenreApiModel?>(key, async _ =>
        {
            try { return await _repo.GetById(id); }
            catch { return null; }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = GenreTags
        }, ct);
    }

    public async Task<IEnumerable<GenreApiModel>> GetAllGenresAsync(CancellationToken ct)
    {
        var key = _keys.Compose(
            moduleName: "administration",
            entity: "genre",
            version: "v1",
            discriminator: "all");

        return await _cache.GetOrAddAsync<IEnumerable<GenreApiModel>>(key, async _ =>
        {
            var entities = await _repo.GetAll();
            return entities.Select(g => g.Convert());
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = GenreTags
        }, ct) ?? [];
    }

    public async Task<GenreApiModel?> CreateGenreAsync(string name, CancellationToken ct)
    {
        var entity = new Genre { Name = name };
        var created = await _repo.Add(entity);

        // Invalidate cache
        await _cache.RemoveByTagAsync(GenreTags[0], ct);

        return created.Convert();
    }

    public async Task<bool> UpdateGenreAsync(int id, string name, CancellationToken ct)
    {
        var entity = new Genre { Id = id, Name = name };
        var updated = await _repo.Update(entity);

        if (updated)
        {
            await _cache.RemoveByTagAsync(GenreTags[0], ct);
        }

        return updated;
    }

    public async Task<bool> DeleteGenreAsync(int id, CancellationToken ct)
    {
        var deleted = await _repo.Delete(id);

        if (deleted)
        {
            await _cache.RemoveByTagAsync(GenreTags[0], ct);
        }

        return deleted;
    }
}
```

#### 6.3 Create Genre Endpoints (15 min)

```csharp
// Admin.Module/Endpoints/GenreEndpoints.cs
public static class GenreEndpoints
{
    public static void MapGenreEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/genres", [Authorize] async (
            IGenreService service,
            CancellationToken ct) =>
        {
            var genres = await service.GetAllGenresAsync(ct);
            return Results.Ok(genres);
        })
        .RequireAuthorization("administration.read")
        .WithName("GetAllGenres")
        .WithTags("Administration");

        group.MapGet("/genres/{id:int}", [Authorize] async (
            int id,
            IGenreService service,
            CancellationToken ct) =>
        {
            var genre = await service.GetGenreByIdAsync(id, ct);
            return genre is not null ? Results.Ok(genre) : Results.NotFound();
        })
        .RequireAuthorization("administration.read")
        .WithName("GetGenreById")
        .WithTags("Administration");

        group.MapPost("/genres", [Authorize] async (
            CreateGenreRequest request,
            IGenreService service,
            CancellationToken ct) =>
        {
            var created = await service.CreateGenreAsync(request.Name, ct);
            return Results.Created($"/api/admin/genres/{created!.Id}", created);
        })
        .RequireAuthorization("administration.write")
        .WithName("CreateGenre")
        .WithTags("Administration");
    }
}

public record CreateGenreRequest(string Name);
```

#### 6.4 Exercise: Create Artist Service (10 min)

Participants implement ArtistService following the same pattern.

### Checkpoint

- [ ] Genre service with caching works
- [ ] Cache invalidation on writes
- [ ] Endpoints use services (not repositories directly)

---

## ☕ Afternoon Break (2:45 PM - 3:00 PM)

---

## Session 7: FluentValidation (3:00 PM - 3:45 PM)

**Duration:** 45 minutes

### Objectives

- Integrate FluentValidation
- Create validators for API models
- Handle validation errors with ProblemDetails

### Hands-On Exercises

#### 7.1 Create Genre Validator (15 min)

```csharp
// SharedKernel.Persistence/Validation/GenreValidator.cs
using FluentValidation;

public class GenreValidator : AbstractValidator<GenreApiModel>
{
    public GenreValidator()
    {
        RuleFor(g => g.Name)
            .NotNull()
            .WithMessage("Name is required");

        RuleFor(g => g.Name)
            .MaximumLength(120)
            .WithMessage("Name must not exceed 120 characters");

        RuleFor(g => g.Name)
            .MinimumLength(1)
            .WithMessage("Name must not be empty");
    }
}
```

#### 7.2 Register Validators (5 min)

```csharp
// In PersistenceRegistration.cs or Program.cs
services.AddValidatorsFromAssemblyContaining<GenreValidator>();
```

#### 7.3 Update Service with Validation (15 min)

```csharp
public sealed class GenreService : IGenreService
{
    private readonly IValidator<GenreApiModel> _validator;
    // ... other fields

    public GenreService(
        IGenreRepository repo,
        ICacheFacade cache,
        ICacheKeyComposer keys,
        IValidator<GenreApiModel> validator)  // Add validator
    {
        _repo = repo;
        _cache = cache;
        _keys = keys;
        _validator = validator;
    }

    public async Task<GenreApiModel?> CreateGenreAsync(string name, CancellationToken ct)
    {
        var model = new GenreApiModel { Name = name };

        // Validate before persisting
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = new Genre { Name = name };
        var created = await _repo.Add(entity);

        await _cache.RemoveByTagAsync(GenreTags[0], ct);

        return created.Convert();
    }
}
```

#### 7.4 Handle Validation Errors in Endpoints (10 min)

```csharp
group.MapPost("/genres", [Authorize] async (
    CreateGenreRequest request,
    IGenreService service,
    CancellationToken ct) =>
{
    try
    {
        var created = await service.CreateGenreAsync(request.Name, ct);
        return Results.Created($"/api/admin/genres/{created!.Id}", created);
    }
    catch (ValidationException ex)
    {
        return Results.ValidationProblem(
            ex.Errors.ToDictionary(
                e => e.PropertyName,
                e => new[] { e.ErrorMessage }));
    }
})
.RequireAuthorization("administration.write")
.ProducesValidationProblem();
```

### Checkpoint

- [ ] Validators created and registered
- [ ] Service validates before persistence
- [ ] Endpoints return proper validation errors

---

## Session 8: Rate Limiting & Security (3:45 PM - 4:30 PM)

**Duration:** 45 minutes

### Objectives

- Configure rate limiting middleware
- Apply policies to endpoints
- Understand security headers

### Hands-On Exercises

#### 8.1 Configure Rate Limiting (20 min)

```csharp
// Program.cs
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global policy for public endpoints
    options.AddPolicy("global:public-anon", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // Stricter policy for auth endpoints
    options.AddPolicy("identity:auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0
            }));
});

// In pipeline (after UseRouting, before UseAuthorization)
app.UseRateLimiter();
```

#### 8.2 Apply Rate Limiting to Endpoints (10 min)

```csharp
// Apply to login endpoint
group.MapPost("/login", handler)
    .RequireRateLimiting("identity:auth");

// Apply to public health endpoints
group.MapGet("/health", handler)
    .RequireRateLimiting("global:public-anon");

// Disable for specific endpoints
group.MapGet("/.well-known/jwks.json", handler)
    .DisableRateLimiting();
```

#### 8.3 Security Headers Overview (15 min)

Discuss security headers (reference docs/secure-headers-plan.md):

- HSTS (HTTP Strict Transport Security)
- X-Content-Type-Options
- X-Frame-Options / CSP frame-ancestors
- Referrer-Policy
- CORS configuration

```csharp
// CORS configuration in Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000",
            "http://localhost:4200",
            "http://localhost:5173")
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});
```

### Checkpoint

- [ ] Rate limiting configured and working
- [ ] Policies applied to appropriate endpoints
- [ ] CORS configured for development

---

## Session 9: Testing & Wrap-Up (4:30 PM - 5:00 PM)

**Duration:** 30 minutes

### Objectives

- Write integration tests with WebApplicationFactory
- Review key concepts
- Q&A

### Hands-On: Write Tests (20 min)

#### 9.1 Basic Integration Test

```csharp
// tests/ModularMonolith.Api.Tests/HealthEndpointTests.cs
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReportingHealth_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/reporting/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MusicHealth_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/music/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

#### 9.2 Authenticated Test

```csharp
[Fact]
public async Task GetGenres_WithValidToken_ReturnsOk()
{
    var client = _factory.CreateClient();

    // Login first
    var loginResponse = await client.PostAsJsonAsync("/api/identity/login",
        new { username = "admin", password = "admin123!" });
    var tokens = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
    var accessToken = tokens.GetProperty("access_token").GetString();

    // Use token
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", accessToken);

    var response = await client.GetAsync("/api/admin/genres");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task GetGenres_WithoutToken_ReturnsUnauthorized()
{
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/admin/genres");

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

### 9.3 Wrap-Up & Q&A (10 min)

**Key Takeaways:**

1. **Modular Monolith** provides clear boundaries without microservices
   complexity
2. **IModule contract** enables clean composition and module isolation
3. **Service layer** encapsulates business logic, caching, and validation
4. **Repository pattern** abstracts data access and enables testing
5. **FluentValidation** provides flexible, testable validation
6. **Rate limiting** protects your API from abuse

**Next Steps:**

- Explore the complete solution in the main branch
- Review the `/docs` folder for detailed documentation
- Consider adding: logging, OpenTelemetry, Redis caching

**Resources:**

- [ASP.NET Core Minimal APIs](https://docs.microsoft.com/aspnet/core/fundamentals/minimal-apis)
- [FluentValidation](https://docs.fluentvalidation.net/)
- [Modular Monolith Pattern](https://www.kamilgrzybek.com/design/modular-monolith-primer/)

---

## Appendix A: Starter Solution Setup Script

For instructors setting up the workshop starter solution:

```bash
#!/bin/bash
# Create workshop starter from complete solution

# Create starter directory
mkdir -p workshop-starter

# Copy structure
cp -r src workshop-starter/
cp -r tests workshop-starter/
cp *.sln workshop-starter/
cp *.props workshop-starter/
cp -r data workshop-starter/

# Clear module implementations (keep just the project files)
find workshop-starter/src/Modules -name "*.cs" -delete

# Keep SharedKernel.Persistence but clear DataSQLite implementations
find workshop-starter/src/Shared/SharedKernel.DataSQLite/Repositories -name "*.cs" -delete

# Keep basic Program.cs but strip module composition
# (Manual step - instructor should prepare a minimal Program.cs)

# Remove test implementations
find workshop-starter/tests -name "*.cs" ! -name "GlobalUsings.cs" -delete
```

---

## Appendix B: Troubleshooting

### Common Issues

**Build Errors:**

- Ensure .NET 10 SDK is installed
- Run `dotnet restore` before building
- Check project references in solution

**Authentication Issues:**

- Verify JWT configuration in appsettings.json
- Check token expiration
- Ensure JWKS endpoint is accessible

**Database Issues:**

- Verify chinook.db exists in data/ folder
- Check connection string in appsettings.json
- Ensure SQLite provider is installed

**Rate Limiting:**

- Rate limits reset after window expires
- Use different IP or wait for reset during testing
- Check if policy is applied to endpoint

---

## Appendix C: Complete Code References

The complete implementation is available in the main branch:

- Repository pattern: `src/Shared/SharedKernel.DataSQLite/Repositories/`
- Services: `src/Modules/*/Services/`
- Validators: `src/Shared/SharedKernel.Persistence/Validation/`
- Identity: `src/Modules/Identity/Identity.Module/`
- Tests: `tests/ModularMonolith.Api.Tests/`

---

## Feedback

Please provide feedback on this workshop:

- What worked well?
- What could be improved?
- What additional topics would you like covered?

Thank you for attending!
