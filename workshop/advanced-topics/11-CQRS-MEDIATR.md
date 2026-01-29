# CQRS with MediatR Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

CQRS (Command Query Responsibility Segregation) separates read and write
operations into distinct models. MediatR provides an elegant in-process
messaging pattern to implement CQRS in .NET applications.

**Duration:** 60-90 minutes
**Prerequisites:** Intermediate C#, understanding of design patterns

---

## Learning Objectives

By the end of this guide, you will:

- Understand CQRS principles and benefits
- Implement commands and queries with MediatR
- Add cross-cutting concerns with behaviors
- Integrate validation into the pipeline
- Test handlers in isolation

---

## 1. Understanding CQRS

### Traditional vs CQRS

```
Traditional Architecture:
┌─────────────────────────────────────┐
│            Service Layer            │
│  (Same model for reads and writes)  │
└─────────────────────────────────────┘

CQRS Architecture:
┌──────────────────┐  ┌──────────────────┐
│  Query Handler   │  │ Command Handler  │
│  (Optimized for  │  │  (Business logic │
│   fast reads)    │  │   and validation)│
└──────────────────┘  └──────────────────┘
```

### When to Use CQRS

| Use CQRS When                 | Avoid CQRS When              |
|-------------------------------|------------------------------|
| Complex domain logic          | Simple CRUD operations       |
| Different read/write patterns | Small applications           |
| Scaling reads independently   | Team unfamiliar with pattern |
| Event sourcing                | Tight deadlines              |

---

## 2. Setting Up MediatR

### Install Package

```xml
<PackageReference Include="MediatR" Version="12.2.0" />
```

### Basic Configuration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register MediatR
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
});

var app = builder.Build();
```

---

## 3. Commands (Write Operations)

### Command Definition

```csharp
// Commands/CreateAlbumCommand.cs
public record CreateAlbumCommand(
    string Title,
    int ArtistId,
    int? GenreId = null) : IRequest<CreateAlbumResult>;

public record CreateAlbumResult(
    int Id,
    string Title,
    string ArtistName);
```

### Command Handler

```csharp
// Commands/CreateAlbumCommandHandler.cs
public class CreateAlbumCommandHandler : IRequestHandler<CreateAlbumCommand, CreateAlbumResult>
{
    private readonly AppDbContext _context;
    private readonly ILogger<CreateAlbumCommandHandler> _logger;

    public CreateAlbumCommandHandler(
        AppDbContext context,
        ILogger<CreateAlbumCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CreateAlbumResult> Handle(
        CreateAlbumCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating album: {Title}", request.Title);

        // Validate artist exists
        var artist = await _context.Artists
            .FindAsync(new object[] { request.ArtistId }, cancellationToken);

        if (artist is null)
        {
            throw new NotFoundException($"Artist {request.ArtistId} not found");
        }

        // Create album
        var album = new Album
        {
            Title = request.Title,
            ArtistId = request.ArtistId
        };

        _context.Albums.Add(album);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Album created: {Id}", album.AlbumId);

        return new CreateAlbumResult(
            album.AlbumId,
            album.Title,
            artist.Name);
    }
}
```

### Using Commands in Endpoints

```csharp
app.MapPost("/api/albums", async (
    CreateAlbumCommand command,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(command, ct);
    return TypedResults.Created($"/api/albums/{result.Id}", result);
});
```

---

## 4. Queries (Read Operations)

### Query Definition

```csharp
// Queries/GetAlbumByIdQuery.cs
public record GetAlbumByIdQuery(int Id) : IRequest<AlbumDetailResult?>;

public record AlbumDetailResult(
    int Id,
    string Title,
    ArtistSummary Artist,
    IEnumerable<TrackSummary> Tracks);

public record ArtistSummary(int Id, string Name);
public record TrackSummary(int Id, string Name, int DurationMs);
```

### Query Handler

```csharp
// Queries/GetAlbumByIdQueryHandler.cs
public class GetAlbumByIdQueryHandler : IRequestHandler<GetAlbumByIdQuery, AlbumDetailResult?>
{
    private readonly AppDbContext _context;

    public GetAlbumByIdQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AlbumDetailResult?> Handle(
        GetAlbumByIdQuery request,
        CancellationToken cancellationToken)
    {
        var album = await _context.Albums
            .AsNoTracking()
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .Where(a => a.AlbumId == request.Id)
            .Select(a => new AlbumDetailResult(
                a.AlbumId,
                a.Title,
                new ArtistSummary(a.Artist.ArtistId, a.Artist.Name),
                a.Tracks.Select(t => new TrackSummary(t.TrackId, t.Name, t.Milliseconds))))
            .FirstOrDefaultAsync(cancellationToken);

        return album;
    }
}
```

### Using Queries in Endpoints

```csharp
app.MapGet("/api/albums/{id}", async (
    int id,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(new GetAlbumByIdQuery(id), ct);
    return result is not null
        ? TypedResults.Ok(result)
        : TypedResults.NotFound();
});
```

---

## 5. List Queries with Pagination

```csharp
// Queries/GetAlbumsQuery.cs
public record GetAlbumsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    int? ArtistId = null) : IRequest<PagedResult<AlbumListItem>>;

public record AlbumListItem(
    int Id,
    string Title,
    string ArtistName,
    int TrackCount);

public record PagedResult<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

// Queries/GetAlbumsQueryHandler.cs
public class GetAlbumsQueryHandler : IRequestHandler<GetAlbumsQuery, PagedResult<AlbumListItem>>
{
    private readonly AppDbContext _context;

    public GetAlbumsQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AlbumListItem>> Handle(
        GetAlbumsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Albums
            .AsNoTracking()
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(request.Search))
        {
            query = query.Where(a =>
                a.Title.Contains(request.Search) ||
                a.Artist.Name.Contains(request.Search));
        }

        if (request.ArtistId.HasValue)
        {
            query = query.Where(a => a.ArtistId == request.ArtistId);
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var items = await query
            .OrderBy(a => a.Title)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AlbumListItem(
                a.AlbumId,
                a.Title,
                a.Artist.Name,
                a.Tracks.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<AlbumListItem>(
            items,
            request.Page,
            request.PageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)request.PageSize));
    }
}
```

---

## 6. Pipeline Behaviors

### Logging Behavior

```csharp
// Behaviors/LoggingBehavior.cs
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation(
            "Handling {RequestName}: {@Request}",
            requestName,
            request);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            stopwatch.Stop();

            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Error handling {RequestName} after {ElapsedMs}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}

// Registration
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
});
```

### Validation Behavior

```csharp
// Behaviors/ValidationBehavior.cs
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}

// Validator for command
public class CreateAlbumCommandValidator : AbstractValidator<CreateAlbumCommand>
{
    public CreateAlbumCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(x => x.ArtistId)
            .GreaterThan(0);
    }
}

// Registration
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
```

### Caching Behavior

```csharp
// Behaviors/CachingBehavior.cs
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan? CacheDuration { get; }
}

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICacheFacade _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        ICacheFacade cache,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery cacheableQuery)
        {
            return await next();
        }

        var cached = await _cache.GetAsync<TResponse>(
            cacheableQuery.CacheKey,
            cancellationToken);

        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for {CacheKey}", cacheableQuery.CacheKey);
            return cached;
        }

        _logger.LogDebug("Cache miss for {CacheKey}", cacheableQuery.CacheKey);

        var response = await next();

        await _cache.SetAsync(
            cacheableQuery.CacheKey,
            response,
            new CacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    cacheableQuery.CacheDuration ?? TimeSpan.FromMinutes(5)
            },
            cancellationToken);

        return response;
    }
}

// Cacheable query example
public record GetGenresQuery : IRequest<IEnumerable<GenreDto>>, ICacheableQuery
{
    public string CacheKey => "genres:all";
    public TimeSpan? CacheDuration => TimeSpan.FromHours(1);
}
```

### Transaction Behavior

```csharp
// Behaviors/TransactionBehavior.cs
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly AppDbContext _context;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        AppDbContext context,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only wrap commands in transactions
        if (!typeof(TRequest).Name.EndsWith("Command"))
        {
            return await next();
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next();

            await transaction.CommitAsync(cancellationToken);

            _logger.LogDebug("Transaction committed for {RequestType}", typeof(TRequest).Name);

            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning("Transaction rolled back for {RequestType}", typeof(TRequest).Name);
            throw;
        }
    }
}
```

---

## 7. Notifications (Events)

### Domain Events

```csharp
// Notifications/AlbumCreatedNotification.cs
public record AlbumCreatedNotification(
    int AlbumId,
    string Title,
    int ArtistId) : INotification;

// Handlers
public class AlbumCreatedEmailHandler : INotificationHandler<AlbumCreatedNotification>
{
    private readonly IEmailService _emailService;

    public AlbumCreatedEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Handle(
        AlbumCreatedNotification notification,
        CancellationToken cancellationToken)
    {
        await _emailService.SendNewAlbumNotificationAsync(
            notification.AlbumId,
            notification.Title,
            cancellationToken);
    }
}

public class AlbumCreatedCacheHandler : INotificationHandler<AlbumCreatedNotification>
{
    private readonly ICacheFacade _cache;

    public AlbumCreatedCacheHandler(ICacheFacade cache)
    {
        _cache = cache;
    }

    public async Task Handle(
        AlbumCreatedNotification notification,
        CancellationToken cancellationToken)
    {
        // Invalidate album caches
        await _cache.RemoveByTagAsync("albums", cancellationToken);
    }
}
```

### Publishing Notifications

```csharp
// In command handler
public class CreateAlbumCommandHandler : IRequestHandler<CreateAlbumCommand, CreateAlbumResult>
{
    private readonly AppDbContext _context;
    private readonly IPublisher _publisher;

    public CreateAlbumCommandHandler(AppDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<CreateAlbumResult> Handle(
        CreateAlbumCommand request,
        CancellationToken cancellationToken)
    {
        // Create album...
        var album = new Album { Title = request.Title, ArtistId = request.ArtistId };
        _context.Albums.Add(album);
        await _context.SaveChangesAsync(cancellationToken);

        // Publish notification
        await _publisher.Publish(
            new AlbumCreatedNotification(album.AlbumId, album.Title, album.ArtistId),
            cancellationToken);

        return new CreateAlbumResult(album.AlbumId, album.Title, "");
    }
}
```

---

## 8. Complete Module Structure

```
src/Modules/Music/Music.Module/
├── Commands/
│   ├── CreateAlbumCommand.cs
│   ├── CreateAlbumCommandHandler.cs
│   ├── CreateAlbumCommandValidator.cs
│   ├── UpdateAlbumCommand.cs
│   ├── UpdateAlbumCommandHandler.cs
│   ├── DeleteAlbumCommand.cs
│   └── DeleteAlbumCommandHandler.cs
├── Queries/
│   ├── GetAlbumByIdQuery.cs
│   ├── GetAlbumByIdQueryHandler.cs
│   ├── GetAlbumsQuery.cs
│   └── GetAlbumsQueryHandler.cs
├── Notifications/
│   ├── AlbumCreatedNotification.cs
│   └── AlbumCreatedHandlers.cs
├── Results/
│   ├── AlbumDetailResult.cs
│   └── AlbumListItem.cs
└── Endpoints/
    └── AlbumEndpoints.cs
```

---

## 9. Endpoint Integration

```csharp
// Endpoints/AlbumEndpoints.cs
public static class AlbumEndpoints
{
    public static void MapAlbumEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/albums").WithTags("Albums");

        // Queries
        group.MapGet("/", GetAlbums);
        group.MapGet("/{id:int}", GetAlbumById);

        // Commands
        group.MapPost("/", CreateAlbum);
        group.MapPut("/{id:int}", UpdateAlbum);
        group.MapDelete("/{id:int}", DeleteAlbum);
    }

    private static async Task<Ok<PagedResult<AlbumListItem>>> GetAlbums(
        [AsParameters] GetAlbumsQuery query,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<AlbumDetailResult>, NotFound>> GetAlbumById(
        int id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetAlbumByIdQuery(id), ct);
        return result is not null
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();
    }

    private static async Task<Created<CreateAlbumResult>> CreateAlbum(
        CreateAlbumCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return TypedResults.Created($"/api/albums/{result.Id}", result);
    }

    private static async Task<Results<Ok<UpdateAlbumResult>, NotFound>> UpdateAlbum(
        int id,
        UpdateAlbumCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command with { Id = id }, ct);
        return result is not null
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAlbum(
        int id,
        IMediator mediator,
        CancellationToken ct)
    {
        var deleted = await mediator.Send(new DeleteAlbumCommand(id), ct);
        return deleted
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }
}
```

---

## 10. Testing Handlers

```csharp
public class CreateAlbumCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesAlbum_WhenValidRequest()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;

        await using var context = new AppDbContext(options);

        // Seed artist
        context.Artists.Add(new Artist { ArtistId = 1, Name = "Test Artist" });
        await context.SaveChangesAsync();

        var handler = new CreateAlbumCommandHandler(
            context,
            Mock.Of<ILogger<CreateAlbumCommandHandler>>());

        var command = new CreateAlbumCommand("Test Album", 1);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal("Test Album", result.Title);

        var saved = await context.Albums.FindAsync(result.Id);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenArtistNotFound()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("TestDb2")
            .Options;

        await using var context = new AppDbContext(options);

        var handler = new CreateAlbumCommandHandler(
            context,
            Mock.Of<ILogger<CreateAlbumCommandHandler>>());

        var command = new CreateAlbumCommand("Test Album", 999);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, CancellationToken.None));
    }
}

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_ThrowsValidationException_WhenInvalid()
    {
        // Arrange
        var validator = new CreateAlbumCommandValidator();
        var behavior = new ValidationBehavior<CreateAlbumCommand, CreateAlbumResult>(
            new[] { validator });

        var invalidCommand = new CreateAlbumCommand("", 0); // Invalid

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(
                invalidCommand,
                () => Task.FromResult(new CreateAlbumResult(1, "", "")),
                CancellationToken.None));
    }
}
```

---

## Summary

### CQRS Components

| Component    | Purpose                           |
|--------------|-----------------------------------|
| Command      | Write operation (changes state)   |
| Query        | Read operation (returns data)     |
| Handler      | Implements command/query logic    |
| Behavior     | Cross-cutting concerns (pipeline) |
| Notification | Domain events                     |

### Pipeline Behavior Order

```
Request → Logging → Validation → Caching → Transaction → Handler → Response
```

### Best Practices

1. **Keep handlers focused** — One handler, one responsibility
2. **Use behaviors for cross-cutting** — Logging, validation, transactions
3. **Commands return minimal data** — Just what's needed to confirm success
4. **Queries return DTOs** — Not entities
5. **Validate early** — In behavior pipeline
6. **Test handlers in isolation** — Mock dependencies

### Registration

```csharp
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssemblyContaining<Program>();
    config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
    config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
```
