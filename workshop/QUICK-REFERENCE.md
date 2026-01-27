# Workshop Quick Reference Card

Keep this handy during the workshop for common patterns and commands.

---

## Commands

```bash
# Build
dotnet build

# Run API
dotnet run --project src/ModularMonolith.Api

# Run tests
dotnet test

# Watch mode (auto-rebuild)
dotnet watch --project src/ModularMonolith.Api
```

---

## API URLs

| Endpoint | URL |
|----------|-----|
| Swagger UI | http://localhost:5043/swagger |
| Root Health | http://localhost:5043/ |
| Reporting Health | http://localhost:5043/api/reporting/health |
| Music Health | http://localhost:5043/api/music/health |
| Login | POST http://localhost:5043/api/identity/login |
| JWKS | http://localhost:5043/api/identity/.well-known/jwks.json |

---

## Demo Users

| Username | Password | Permissions |
|----------|----------|-------------|
| demo | demo123! | music.read |
| admin | admin123! | music.*, orders.*, administration.* |
| usermo | usermo123! | music.read, orders.read |
| report | report123! | report.view |

---

## Module Contract

```csharp
public interface IModule
{
    string Name { get; }
    void RegisterServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

---

## Module Implementation Template

```csharp
namespace MyModule.Modules;

public static class MyModule
{
    public sealed class Modules : IModule
    {
        public string Name => "MyModule";

        public void RegisterServices(IServiceCollection services, IConfiguration config)
        {
            services.AddScoped<IMyService, MyService>();
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/mymodule");
            group.MapMyEndpoints();
        }
    }
}
```

---

## Endpoint Patterns

### GET (Read)
```csharp
group.MapGet("/items/{id:int}", [Authorize] async (
    int id,
    IMyService service,
    CancellationToken ct) =>
{
    var item = await service.GetByIdAsync(id, ct);
    return item is not null ? Results.Ok(item) : Results.NotFound();
})
.RequireAuthorization("module.read")
.WithName("GetItemById")
.WithTags("MyModule")
.Produces<ItemModel>(200)
.Produces(404);
```

### POST (Create)
```csharp
group.MapPost("/items", [Authorize] async (
    CreateRequest request,
    IMyService service,
    CancellationToken ct) =>
{
    try
    {
        var created = await service.CreateAsync(request, ct);
        return Results.Created($"/api/mymodule/items/{created.Id}", created);
    }
    catch (ValidationException ex)
    {
        return Results.ValidationProblem(
            ex.Errors.ToDictionary(e => e.PropertyName, e => new[] { e.ErrorMessage }));
    }
})
.RequireAuthorization("module.write")
.WithName("CreateItem")
.ProducesValidationProblem();
```

---

## Service Pattern

```csharp
public sealed class MyService : IMyService
{
    private readonly IMyRepository _repo;
    private readonly ICacheFacade _cache;
    private readonly ICacheKeyComposer _keys;
    private readonly IValidator<MyModel> _validator;

    private static readonly string[] Tags = ["module:entity"];

    public MyService(
        IMyRepository repo,
        ICacheFacade cache,
        ICacheKeyComposer keys,
        IValidator<MyModel> validator)
    {
        _repo = repo;
        _cache = cache;
        _keys = keys;
        _validator = validator;
    }

    // Read with caching
    public async Task<MyModel?> GetByIdAsync(int id, CancellationToken ct)
    {
        var key = _keys.Compose("module", "entity", "v1", $"by-id:{id}");

        return await _cache.GetOrAddAsync<MyModel?>(key, async _ =>
        {
            return await _repo.GetById(id);
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = Tags
        }, ct);
    }

    // Write with validation and cache invalidation
    public async Task<MyModel?> CreateAsync(CreateRequest request, CancellationToken ct)
    {
        var model = new MyModel { Name = request.Name };

        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);

        var created = await _repo.Add(model.ToEntity());
        await _cache.RemoveByTagAsync(Tags[0], ct);

        return created.ToModel();
    }
}
```

---

## Repository Pattern

```csharp
public abstract class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _context;

    public virtual async Task<List<T>> GetAll()
        => await _context.Set<T>().AsNoTracking().ToListAsync();

    public virtual async Task<T> Add(T entity)
    {
        await _context.Set<T>().AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task<bool> Update(T entity)
    {
        _context.Set<T>().Update(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public virtual async Task<bool> Delete(int id)
    {
        var entity = await _context.Set<T>().FindAsync(id);
        if (entity is null) return false;

        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
```

---

## FluentValidation

```csharp
public class MyModelValidator : AbstractValidator<MyModel>
{
    public MyModelValidator()
    {
        RuleFor(x => x.Name)
            .NotNull().WithMessage("Name is required")
            .MaximumLength(120).WithMessage("Name too long");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Invalid email");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be positive")
            .LessThanOrEqualTo(9.99m).WithMessage("Price exceeds maximum");
    }
}

// Register all validators
services.AddValidatorsFromAssemblyContaining<MyModelValidator>();
```

---

## Cache Key Composition

```csharp
// Key format: {env}:{app}:{module}:{entity}:{version}:{discriminator}
var key = keys.Compose(
    moduleName: "music",
    entity: "album",
    version: "v1",
    discriminator: $"by-id:{id}");
// Result: prod:mmapi:music:album:v1:by-id:42
```

---

## Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("global:public", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromSeconds(60)
            }));
});

// Apply to endpoint
.RequireRateLimiting("global:public")
```

---

## JWT Claims

```csharp
// In endpoint handler
var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
var tenant = context.User.FindFirst("tenant")?.Value;
var permissions = context.User.FindAll("permissions").Select(c => c.Value);
```

---

## Authorization Policies

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("music.read", policy =>
        policy.RequireClaim("permissions", "music.read"));

    options.AddPolicy("tenant.scoped", policy =>
        policy.Requirements.Add(new TenantRequirement()));
});

// Apply to endpoint
.RequireAuthorization("music.read")
.RequireAuthorization("tenant.scoped")
```

---

## Test Patterns

```csharp
public class MyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MyTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/mymodule/items");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProtected_WithToken_ReturnsOk()
    {
        // Get token
        var loginResponse = await _client.PostAsJsonAsync("/api/identity/login",
            new { username = "admin", password = "admin123!" });
        var tokens = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = tokens.GetProperty("access_token").GetString();

        // Use token
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/mymodule/items");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

---

## HTTP Status Codes

| Code | Meaning | When to Use |
|------|---------|-------------|
| 200 | OK | Successful GET/PUT |
| 201 | Created | Successful POST |
| 204 | No Content | Successful DELETE |
| 400 | Bad Request | Validation errors |
| 401 | Unauthorized | Missing/invalid token |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource doesn't exist |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Server Error | Unexpected errors |

---

## Useful Curl Commands

```bash
# Health check
curl http://localhost:5043/api/music/health

# Login
curl -X POST http://localhost:5043/api/identity/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123!"}'

# Authenticated request
TOKEN="<paste-token-here>"
curl http://localhost:5043/api/admin/genres \
  -H "Authorization: Bearer $TOKEN"

# Create resource
curl -X POST http://localhost:5043/api/admin/genres \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"New Genre"}'
```
