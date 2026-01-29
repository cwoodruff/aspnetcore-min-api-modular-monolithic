# Session 7: FluentValidation

**Duration:** 45 minutes
**Session Time:** 3:00 PM - 3:45 PM

---

## Overview

This session covers input validation using FluentValidation. You'll learn to
create validators for API models, register them with dependency injection, and
handle validation errors with proper problem details responses.

---

## Learning Objectives

By the end of this session, you will:

- Create validators using FluentValidation
- Apply common validation rules
- Register validators with DI
- Handle validation exceptions in services
- Return RFC 7807 problem details responses

---

## Part 1: FluentValidation Basics (10 minutes)

### 1.1 Why FluentValidation?

| Benefit         | Description                           |
|-----------------|---------------------------------------|
| **Separation**  | Validation logic separate from models |
| **Testability** | Validators are easily unit tested     |
| **Readability** | Fluent API for rule definition        |
| **Flexibility** | Complex conditional rules supported   |

### 1.2 Package Reference

FluentValidation is included via NuGet:

```xml
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.x" />
```

---

## Part 2: Validator Implementations (20 minutes)

### 2.1 Album Validator

**File: `src/Shared/SharedKernel.Persistence/Validation/AlbumValidator.cs`**

```csharp
using FluentValidation;
using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Validation;

public class AlbumValidator : AbstractValidator<AlbumApiModel>
{
    public AlbumValidator()
    {
        RuleFor(a => a.Title).NotNull();
        RuleFor(a => a.Title).MinimumLength(3);
        RuleFor(a => a.Title).MaximumLength(160);
        RuleFor(a => a.ArtistId).NotNull();
    }
}
```

### 2.2 Customer Validator (Complex Example)

**File: `src/Shared/SharedKernel.Persistence/Validation/CustomerValidator.cs`**

```csharp
using FluentValidation;
using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Validation;

public class CustomerValidator : AbstractValidator<CustomerApiModel>
{
    public CustomerValidator()
    {
        RuleFor(c => c.FirstName).NotNull();
        RuleFor(c => c.LastName).NotNull();
        RuleFor(c => c.Email).EmailAddress();
        RuleFor(c => c.Phone).Matches(@"\(?\d{3}\)?[-\.]? *\d{3}[-\.]? *[-\.]?\d{4}");
        RuleFor(c => c.Fax).Matches(@"\(?\d{3}\)?[-\.]? *\d{3}[-\.]? *[-\.]?\d{4}");
        RuleFor(c => c.FirstName).MaximumLength(40);
        RuleFor(c => c.LastName).MaximumLength(20);
        RuleFor(c => c.Company).MaximumLength(80);
        RuleFor(c => c.Address).MaximumLength(70);
        RuleFor(c => c.City).MaximumLength(40);
        RuleFor(c => c.State).MaximumLength(40);
        RuleFor(c => c.Country).MaximumLength(40);
        RuleFor(c => c.PostalCode).Matches(@"^[0-9]{5}(?:-[0-9]{4})?$");
    }
}
```

### 2.3 Genre Validator (Simple Example)

**File: `src/Shared/SharedKernel.Persistence/Validation/GenreValidator.cs`**

```csharp
using FluentValidation;
using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Validation;

public class GenreValidator : AbstractValidator<GenreApiModel>
{
    public GenreValidator()
    {
        RuleFor(g => g.Name).NotNull();
        RuleFor(g => g.Name).MaximumLength(120);
    }
}
```

### 2.4 Common Validation Rules

| Rule                     | Usage                       | Example                                        |
|--------------------------|-----------------------------|------------------------------------------------|
| `NotNull()`              | Required field              | `RuleFor(x => x.Name).NotNull()`               |
| `NotEmpty()`             | Not null and not whitespace | `RuleFor(x => x.Name).NotEmpty()`              |
| `MinimumLength(n)`       | Min string length           | `RuleFor(x => x.Name).MinimumLength(3)`        |
| `MaximumLength(n)`       | Max string length           | `RuleFor(x => x.Name).MaximumLength(100)`      |
| `EmailAddress()`         | Valid email format          | `RuleFor(x => x.Email).EmailAddress()`         |
| `Matches(regex)`         | Regex pattern               | `RuleFor(x => x.Phone).Matches(@"\d{10}")`     |
| `GreaterThan(n)`         | Numeric comparison          | `RuleFor(x => x.Age).GreaterThan(0)`           |
| `InclusiveBetween(a, b)` | Range                       | `RuleFor(x => x.Age).InclusiveBetween(1, 120)` |

---

## Part 3: Validator Registration (5 minutes)

### 3.1 Assembly Scanning Registration

**File: `src/Shared/SharedKernel.Persistence/PersistenceRegistration.cs` (
partial)**

```csharp
using FluentValidation;
using SharedKernel.Persistence.Validation;

public static class PersistenceRegistration
{
    public static IServiceCollection AddKernelPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // ... other registrations

        // Register FluentValidation validators from assembly
        services.AddValidatorsFromAssemblyContaining<CustomerValidator>();

        return services;
    }
}
```

### 3.2 How Assembly Scanning Works

```csharp
services.AddValidatorsFromAssemblyContaining<CustomerValidator>();
```

This registers:

- All classes inheriting `AbstractValidator<T>`
- As `IValidator<T>` interfaces
- With `Scoped` lifetime (default)

---

## Part 4: Using Validators in Services (10 minutes)

### 4.1 Service Constructor Injection

**File: `src/Modules/Music/Music.Module/Services/AlbumService.cs` (partial)**

```csharp
public class AlbumService(
    IAlbumRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<AlbumApiModel> validator) : IAlbumService
{
    private readonly IValidator<AlbumApiModel> _validator = validator;
    // ...
}
```

### 4.2 Validation Before Persistence

```csharp
public async Task<AlbumApiModel?> CreateAlbumAsync(AlbumApiModel model, CancellationToken ct)
{
    // Validate input
    var result = await _validator.ValidateAsync(model, ct);
    if (!result.IsValid)
    {
        throw new ValidationException(result.Errors);
    }

    // Proceed with creation
    var entity = model.Convert();
    var created = await repository.Add(entity);

    // Invalidate cache
    await cache.RemoveByTagAsync(AlbumTags[0], ct);

    return created?.Convert();
}

public async Task<bool> UpdateAlbumAsync(AlbumApiModel model, CancellationToken ct)
{
    // Validate input
    var result = await _validator.ValidateAsync(model, ct);
    if (!result.IsValid)
    {
        throw new ValidationException(result.Errors);
    }

    // Proceed with update
    var entity = model.Convert();
    var updated = await repository.Update(entity);

    if (updated)
    {
        await cache.RemoveByTagAsync(AlbumTags[0], ct);
        var key = keys.Compose(
            moduleName: "music",
            entity: "album",
            version: "v1",
            discriminator: $"by-id:{model.Id}");
        await cache.RemoveAsync(key, ct);
    }

    return updated;
}
```

### 4.3 Validation Flow

```
1. Endpoint receives request
2. Request bound to API model
3. Service method called with model
4. Validator.ValidateAsync(model) runs all rules
5. If invalid: throw ValidationException
6. If valid: proceed with business logic
```

---

## Part 5: Handling Validation Errors

### 5.1 Endpoint Error Handling Pattern

```csharp
group.MapPost("/albums", [Authorize] async (
    AlbumApiModel model,
    IAlbumService service,
    CancellationToken ct) =>
{
    try
    {
        var created = await service.CreateAlbumAsync(model, ct);
        return Results.Created($"/api/music/albums/{created!.Id}", created);
    }
    catch (FluentValidation.ValidationException ex)
    {
        return Results.ValidationProblem(
            ex.Errors.ToDictionary(
                e => e.PropertyName,
                e => new[] { e.ErrorMessage }));
    }
})
.RequireAuthorization("music.write")
.WithName("CreateAlbum")
.Produces<AlbumApiModel>(StatusCodes.Status201Created)
.ProducesValidationProblem()
.WithTags("Music");
```

### 5.2 ValidationProblem Response

The `Results.ValidationProblem()` returns RFC 7807 format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Title": ["Title must be at least 3 characters."],
    "ArtistId": ["ArtistId is required."]
  }
}
```

### 5.3 OpenAPI Documentation

```csharp
.ProducesValidationProblem()
```

This adds 400 response documentation to Swagger:

- Response type: `ValidationProblemDetails`
- Status code: 400

---

## Checkpoint

Before moving to Session 8, verify:

- [ ] Understand AbstractValidator<T> pattern
- [ ] Know common validation rules
- [ ] Can register validators via assembly scanning
- [ ] Understand validation in service layer
- [ ] Can return proper validation problem responses

---

## Quick Reference

### Validator Template

```csharp
using FluentValidation;

public class YourModelValidator : AbstractValidator<YourApiModel>
{
    public YourModelValidator()
    {
        RuleFor(x => x.RequiredField)
            .NotNull()
            .WithMessage("RequiredField is required.");

        RuleFor(x => x.StringField)
            .MaximumLength(100)
            .When(x => x.StringField != null);

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Number)
            .GreaterThan(0)
            .WithMessage("Number must be positive.");
    }
}
```

### Validation Rules Quick Reference

| Rule                | Description               |
|---------------------|---------------------------|
| `.NotNull()`        | Not null                  |
| `.NotEmpty()`       | Not null/empty/whitespace |
| `.Length(min, max)` | String length range       |
| `.EmailAddress()`   | Valid email format        |
| `.Matches(regex)`   | Regex pattern             |
| `.Must(predicate)`  | Custom validation         |
| `.When(condition)`  | Conditional rule          |
| `.WithMessage(msg)` | Custom error message      |

### Handling in Endpoints

```csharp
try
{
    var result = await service.CreateAsync(model, ct);
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

## Next Session

In **Session 8: Rate Limiting & Security**, you will:

- Configure rate limiting middleware
- Create custom rate limit policies
- Apply policies to endpoints
- Understand security best practices
