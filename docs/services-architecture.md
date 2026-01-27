# Services Architecture

**Status: Implemented**

This document describes the service layer architecture implemented across all modules in the Modular Monolith. Services provide a clean abstraction between endpoints and repositories, encapsulating business logic, validation, and caching.

---

## 1) Executive Summary

The solution implements a **service layer pattern** where each module contains dedicated service classes that:
- Encapsulate business logic and data access orchestration
- Integrate FluentValidation for input validation
- Manage caching with tag-based invalidation
- Provide a clean interface for Minimal API endpoints

All services follow consistent patterns for dependency injection, caching, validation, and error handling.

---

## 2) Architecture Overview

### Data Flow

```
HTTP Request
    ↓
Endpoint (Authorization, Rate Limiting, Model Binding)
    ↓
Service (Validation → Cache Lookup → Repository Call → Cache Store)
    ↓
Repository (EF Core Query → DTO Projection)
    ↓
Service (Return cached/fresh data)
    ↓
HTTP Response (JSON)
```

### Service Responsibilities

1. **Input Validation** - Validate incoming data using FluentValidation before persistence operations
2. **Cache Management** - Implement cache-aside pattern with tag-based invalidation
3. **Repository Orchestration** - Coordinate repository calls for complex operations
4. **Error Handling** - Graceful degradation returning null/empty on failures
5. **DTO Transformation** - Convert between entities and API models

---

## 3) Module Services Inventory

### Administration Module

Located in: `src/Modules/Administration/Admin.Module/Services/`

| Service | Interface | Methods |
|---------|-----------|---------|
| **CustomerService** | `ICustomerService` | `GetCustomerByIdAsync`, `GetAllCustomersAsync`, `GetCustomersBySupportRepIdAsync`, `CreateCustomerAsync`, `UpdateCustomerAsync` |
| **EmployeeService** | `IEmployeeService` | `GetEmployeeByIdAsync`, `GetAllEmployeesAsync`, `GetDirectReportsAsync`, `GetReportsToAsync`, `CreateEmployeeAsync`, `UpdateEmployeeAsync` |
| **GenreService** | `IGenreService` | `GetGenreByIdAsync`, `GetAllGenresAsync`, `CreateGenreAsync`, `UpdateGenreAsync`, `DeleteGenreAsync` |
| **MediaTypeService** | `IMediaTypeService` | `GetMediaTypeByIdAsync`, `GetAllMediaTypesAsync`, `CreateMediaTypeAsync`, `UpdateMediaTypeAsync`, `DeleteMediaTypeAsync` |

### Music Module

Located in: `src/Modules/Music/Music.Module/Services/`

| Service | Interface | Methods |
|---------|-----------|---------|
| **AlbumService** | `IAlbumService` | `GetAlbumByIdAsync`, `GetAllAlbumsAsync`, `GetAlbumsByArtistIdAsync`, `CreateAlbumAsync`, `UpdateAlbumAsync` |
| **ArtistService** | `IArtistService` | `GetArtistByIdAsync`, `GetAllArtistsAsync`, `CreateArtistAsync`, `UpdateArtistAsync` |
| **TrackService** | `ITrackService` | `GetTrackByIdAsync`, `GetAllTracksAsync`, `GetTracksByArtistIdAsync`, `GetTracksByAlbumIdAsync`, `GetTracksByPlaylistIdAsync`, `GetTracksByGenreIdAsync`, `GetTracksByMediaTypeIdAsync`, `GetTracksByInvoiceIdAsync`, `CreateTrackAsync`, `UpdateTrackAsync` |
| **PlaylistService** | `IPlaylistService` | `GetPlaylistByIdAsync`, `GetAllPlaylistsAsync`, `CreatePlaylistAsync`, `UpdatePlaylistAsync` |

### Orders Module

Located in: `src/Modules/Orders/Orders.Module/Services/`

| Service | Interface | Methods |
|---------|-----------|---------|
| **InvoiceService** | `IInvoiceService` | `GetInvoiceByIdAsync`, `GetAllInvoicesAsync`, `GetInvoicesByCustomerIdAsync`, `CreateInvoiceAsync`, `UpdateInvoiceAsync` |
| **InvoiceLineService** | `IInvoiceLineService` | `GetInvoiceLineByIdAsync`, `GetAllInvoiceLinesAsync`, `GetInvoiceLinesByInvoiceIdAsync`, `GetInvoiceLinesByTrackIdAsync`, `CreateInvoiceLineAsync`, `UpdateInvoiceLineAsync` |

### Identity Module

Located in: `src/Modules/Identity/Identity.Module/Services/`

| Service | Interface | Methods |
|---------|-----------|---------|
| **TokenService** | `ITokenService` | `IssueAsync`, `RefreshAsync` |
| **InMemoryUserStore** | `IUserStore` | `ValidateCredentialsAsync` |
| **InMemoryRefreshTokenStore** | `IRefreshTokenStore` | `StoreAsync`, `ValidateAsync`, `RevokeAsync` |

### Reporting Module

The Reporting module currently contains health endpoints only and is extensible for future analytics services.

---

## 4) Service Implementation Pattern

All services follow a consistent implementation pattern:

### Constructor Injection

```csharp
public sealed class CustomerService(
    ICustomerRepository repo,          // Repository for data access
    ICacheFacade cache,                 // Cache facade for caching
    ICacheKeyComposer keys,             // Key composer for structured cache keys
    IValidator<CustomerApiModel> validator  // FluentValidation validator
) : ICustomerService
{
    private readonly IValidator<CustomerApiModel> _validator = validator;
    private static readonly string[] CustomerTags = ["administration:customer", "administration:customer:by-id"];
    // ...
}
```

### Read Operations (with Caching)

```csharp
public async Task<CustomerApiModel?> GetCustomerByIdAsync(int id, CancellationToken ct)
{
    // 1. Compose structured cache key
    var key = keys.Compose(
        moduleName: "administration",
        entity: "customer",
        version: "v1",
        discriminator: $"by-id:{id}");

    // 2. Cache-aside pattern with GetOrAddAsync
    return await cache.GetOrAddAsync<CustomerApiModel?>(key, async _ =>
    {
        try
        {
            return await repo.GetById(id);
        }
        catch
        {
            return null;  // Graceful degradation
        }
    }, new CacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
        Tags = CustomerTags  // For bulk invalidation
    }, ct);
}
```

### Write Operations (with Validation and Cache Invalidation)

```csharp
public async Task<CustomerApiModel?> CreateCustomerAsync(CustomerApiModel model, CancellationToken ct)
{
    // 1. Validate input
    var result = await _validator.ValidateAsync(model, ct);
    if (!result.IsValid)
    {
        throw new ValidationException(result.Errors);
    }

    // 2. Convert to entity and persist
    var entity = model.Convert();
    var created = await repo.Add(entity);

    // 3. Invalidate related cache entries by tag
    await cache.RemoveByTagAsync(CustomerTags[0], ct);

    // 4. Return created model
    return created?.Convert();
}

public async Task<bool> UpdateCustomerAsync(CustomerApiModel model, CancellationToken ct)
{
    // 1. Validate input
    var result = await _validator.ValidateAsync(model, ct);
    if (!result.IsValid)
    {
        throw new ValidationException(result.Errors);
    }

    // 2. Convert and update
    var entity = model.Convert();
    var updated = await repo.Update(entity);

    // 3. Invalidate caches on success
    if (updated)
    {
        await cache.RemoveByTagAsync(CustomerTags[0], ct);  // Bulk invalidate
        var key = keys.Compose(
            moduleName: "administration",
            entity: "customer",
            version: "v1",
            discriminator: $"by-id:{model.Id}");
        await cache.RemoveAsync(key, ct);  // Specific key invalidation
    }

    return updated;
}
```

---

## 5) Service Registration

Services are registered in each module's `Module.cs` file:

### Administration Module Registration

```csharp
public sealed class Modules : IModule
{
    public string Name => "Administration";

    public void RegisterServices(IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IGenreService, GenreService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IMediaTypeService, MediaTypeService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin");
        group.MapCustomerEndpoints();
        group.MapEmployeeEndpoints();
        group.MapGenreEndpoints();
        group.MapMediaTypeEndpoints();
        // ... health endpoints
    }
}
```

### Music Module Registration

```csharp
public void RegisterServices(IServiceCollection services, IConfiguration config)
{
    services.AddScoped<IArtistService, ArtistService>();
    services.AddScoped<IAlbumService, AlbumService>();
    services.AddScoped<ITrackService, TrackService>();
    services.AddScoped<IPlaylistService, PlaylistService>();
}
```

### Orders Module Registration

```csharp
public void RegisterServices(IServiceCollection services, IConfiguration config)
{
    services.AddScoped<IInvoiceService, InvoiceService>();
    services.AddScoped<IInvoiceLineService, InvoiceLineService>();
}
```

---

## 6) Endpoint Integration

Endpoints inject services and delegate business logic:

```csharp
public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/admin/customers/{id}
        group.MapGet("/customers/{id:int}", [Authorize] async (
                int id,
                ICustomerService service,
                CancellationToken ct) =>
            {
                var customer = await service.GetCustomerByIdAsync(id, ct);
                return customer is not null ? Results.Json(customer) : Results.NotFound();
            })
            .RequireAuthorization("administration.read")
            .RequireAuthorization("tenant.scoped")
            .WithName("AdministrationGetCustomerById")
            .Produces<CustomerApiModel>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Administration");

        // POST /api/admin/customers
        group.MapPost("/customers", [Authorize] async (
                CustomerApiModel model,
                ICustomerService service,
                CancellationToken ct) =>
            {
                try
                {
                    var created = await service.CreateCustomerAsync(model, ct);
                    return created is not null
                        ? Results.Created($"/api/admin/customers/{created.Id}", created)
                        : Results.BadRequest();
                }
                catch (ValidationException ex)
                {
                    return Results.ValidationProblem(
                        ex.Errors.ToDictionary(e => e.PropertyName, e => new[] { e.ErrorMessage }));
                }
            })
            .RequireAuthorization("administration.write")
            .WithName("AdministrationCreateCustomer")
            .Produces<CustomerApiModel>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .WithTags("Administration");
    }
}
```

---

## 7) Error Handling Strategy

### Read Operations
- Return `null` for single-entity lookups when not found
- Return empty collections for list operations on failure
- Use try-catch to prevent exceptions from bubbling up

### Write Operations
- Throw `ValidationException` for validation failures (caught by endpoints)
- Return `false` or `null` for failed persistence operations
- Endpoints handle exceptions and return appropriate HTTP responses

### Validation Exceptions

```csharp
try
{
    var created = await service.CreateCustomerAsync(model, ct);
    return Results.Created(...);
}
catch (ValidationException ex)
{
    return Results.ValidationProblem(
        ex.Errors.ToDictionary(
            e => e.PropertyName,
            e => new[] { e.ErrorMessage }));
}
```

---

## 8) Cross-Module Queries

Some services support cross-module data retrieval:

### TrackService (Music Module)
- `GetTracksByInvoiceIdAsync` - Retrieves tracks associated with an invoice (Orders module relationship)

### InvoiceLineService (Orders Module)
- `GetInvoiceLinesByTrackIdAsync` - Retrieves invoice lines for a specific track (Music module relationship)

These cross-module queries use existing foreign key relationships in the Chinook database while maintaining module boundaries at the service interface level.

---

## 9) Cache Tag Strategy

Each service defines cache tags for bulk invalidation:

| Module | Entity | Tags |
|--------|--------|------|
| Administration | Customer | `["administration:customer", "administration:customer:by-id"]` |
| Administration | Employee | `["administration:employee", "administration:employee:by-id"]` |
| Administration | Genre | `["administration:genre", "administration:genre:by-id"]` |
| Administration | MediaType | `["administration:mediatype", "administration:mediatype:by-id"]` |
| Music | Artist | `["music:artist", "music:artist:by-id"]` |
| Music | Album | `["music:album", "music:album:by-id"]` |
| Music | Track | `["music:track", "music:track:by-id"]` |
| Music | Playlist | `["music:playlist", "music:playlist:by-id"]` |
| Orders | Invoice | `["orders:invoice", "orders:invoice:by-id"]` |
| Orders | InvoiceLine | `["orders:invoiceline", "orders:invoiceline:by-id"]` |

---

## 10) Service Lifetime

All services are registered as **Scoped**:
- One instance per HTTP request
- Allows proper EF Core context sharing within a request
- Ensures thread safety for concurrent requests

```csharp
services.AddScoped<ICustomerService, CustomerService>();
```

---

## 11) Testing Services

Services can be tested in isolation by mocking dependencies:

```csharp
public class CustomerServiceTests
{
    [Fact]
    public async Task GetCustomerByIdAsync_ReturnsCustomer_WhenExists()
    {
        // Arrange
        var mockRepo = new Mock<ICustomerRepository>();
        var mockCache = new Mock<ICacheFacade>();
        var mockKeys = new Mock<ICacheKeyComposer>();
        var mockValidator = new Mock<IValidator<CustomerApiModel>>();

        mockCache.Setup(c => c.GetOrAddAsync(
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task<CustomerApiModel?>>>(),
            It.IsAny<CacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerApiModel { Id = 1, FirstName = "Test" });

        var service = new CustomerService(
            mockRepo.Object, mockCache.Object, mockKeys.Object, mockValidator.Object);

        // Act
        var result = await service.GetCustomerByIdAsync(1, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }
}
```

---

## 12) Best Practices

1. **Keep Services Focused** - Each service handles one entity type
2. **Use Interfaces** - All services have corresponding interfaces for testability
3. **Consistent Naming** - `Get*Async`, `Create*Async`, `Update*Async`, `Delete*Async`
4. **Validation First** - Always validate before persistence operations
5. **Cache Invalidation** - Invalidate affected cache entries after writes
6. **Graceful Degradation** - Return null/empty rather than throwing on read failures
7. **Structured Cache Keys** - Use `ICacheKeyComposer` for consistent key composition
8. **Tag-Based Invalidation** - Use tags for efficient bulk cache clearing

---

## 13) Future Considerations

- **Unit of Work Pattern** - Consider adding `IUnitOfWork` for complex multi-entity transactions
- **Domain Events** - Add domain event publishing for cross-module notifications
- **Mediator Pattern** - Consider MediatR for decoupling endpoint handlers from services
- **Read/Write Segregation** - Split read and write services for CQRS-style architecture
- **Specification Pattern** - Add specifications for complex query filtering

---

## 14) Related Documentation

- [Validation Strategy](validation-strategy.md) - FluentValidation implementation details
- [Caching Strategy](caching-strategy.md) - Cache configuration and patterns
- [EF Core Plan](EFCore-Plan.md) - Database and repository architecture
- [Authentication & Authorization](authn-authz-plan.md) - Security implementation
