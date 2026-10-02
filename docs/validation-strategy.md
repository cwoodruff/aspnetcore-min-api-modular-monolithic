# Validation Strategy

**Status: Implemented**

This document describes the validation architecture implemented in the Modular Monolith using FluentValidation. Each module owns its validators (internal, in its `Validation/` folder) and its services consume them.

---

## 1) Executive Summary

Validation happens in two places, with one error shape:

- **Module-owned validators** in each module's `Validation/` folder. Catalog, Orders and Administration use
  FluentValidation; Identity's three request bodies use small hand-written validators.
- **Endpoint filter first** - write endpoints with a body add `ValidationFilter<T>` (SharedKernel), which runs
  the module's `IRequestValidator<T>` before the handler and answers 400 if it fails (phase 7).
- **Service-layer validation as the fallback** - services still validate their models and throw
  `ValidationException`; the host's exception handler turns it into the same 400.
- **One error shape** - RFC 7807 validation problem: `title` "Request validation failed.", `detail`,
  `type`, `errors` by property, and `traceId`, whichever path caught it.

---

## 2) Architecture Overview

### Validation Flow

```
HTTP request (JSON body)
    ↓
Model binding (malformed JSON → 400 "Malformed request.")
    ↓
ValidationFilter<T>  ── invalid → 400 validation problem (handler never runs)
    ↓ valid
Handler (static method on XHandlers)
    ↓
Service method → IValidator<T>.ValidateAsync
    ├─ valid: continue to the module's DbContext
    └─ invalid: throw ValidationException → host exception handler → same 400 validation problem
```

### Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **Validators** | Define the rules for a module's request and API models |
| **`IRequestValidator<T>`** | SharedKernel's library-neutral interface the filter calls; a module adapts its validators to it (Administration: `FluentRequestValidator<T>`) |
| **`ValidationFilter<T>`** | Rejects an invalid body before the handler runs; fails closed if no validator is registered |
| **Services** | Validate again before persistence, so a call that bypasses the filter is still checked |
| **Host** (`Program.WriteProblemDetailsResponseAsync`) | Maps `ValidationException` to the same 400 body the filter writes |

---

## 3) Validator Implementation

### Location

Each module keeps its validators, `internal`, in its own folder:
```
src/Modules/Catalog/Catalog.Module/Validation/
src/Modules/Orders/Orders.Module/Validation/
src/Modules/Administration/Admin.Module/Validation/
```

### Validator Registry

| Validator | Model | Module |
|-----------|-------|--------|
| `CustomerValidator` | `CustomerApiModel` | Administration |
| `EmployeeValidator` | `EmployeeApiModel` | Administration |
| `GenreValidator` | `GenreApiModel` | Administration |
| `MediaTypeValidator` | `MediaTypeApiModel` | Administration |
| `ArtistValidator` | `ArtistApiModel` | Catalog |
| `AlbumValidator` | `AlbumApiModel` | Catalog |
| `TrackValidator` | `TrackApiModel` | Catalog |
| `PlaylistValidator` | `PlaylistApiModel` | Catalog |
| `InvoiceValidator` | `InvoiceApiModel` | Orders |
| `InvoiceLineValidator` | `InvoiceLineApiModel` | Orders |

---

## 4) Validator Examples

### Simple Validator (GenreValidator)

```csharp
public class GenreValidator : AbstractValidator<GenreApiModel>
{
    public GenreValidator()
    {
        RuleFor(g => g.Name).NotNull();
        RuleFor(g => g.Name).MaximumLength(120);
    }
}
```

### Complex Validator (CustomerValidator)

```csharp
public class CustomerValidator : AbstractValidator<CustomerApiModel>
{
    public CustomerValidator()
    {
        // Required fields
        RuleFor(c => c.FirstName).NotNull();
        RuleFor(c => c.LastName).NotNull();

        // Email validation
        RuleFor(c => c.Email).EmailAddress();

        // Phone/Fax regex validation
        RuleFor(c => c.Phone).Matches(@"\(?\d{3}\)?[-\.]? *\d{3}[-\.]? *[-\.]?\d{4}");
        RuleFor(c => c.Fax).Matches(@"\(?\d{3}\)?[-\.]? *\d{3}[-\.]? *[-\.]?\d{4}");

        // Length constraints
        RuleFor(c => c.FirstName).MaximumLength(40);
        RuleFor(c => c.LastName).MaximumLength(20);
        RuleFor(c => c.Company).MaximumLength(80);
        RuleFor(c => c.Address).MaximumLength(70);
        RuleFor(c => c.City).MaximumLength(40);
        RuleFor(c => c.State).MaximumLength(40);
        RuleFor(c => c.Country).MaximumLength(40);

        // Postal code regex
        RuleFor(c => c.PostalCode).Matches(@"^[0-9]{5}(?:-[0-9]{4})?$");
    }
}
```

### Business Rule Validator (TrackValidator)

```csharp
public class TrackValidator : AbstractValidator<TrackApiModel>
{
    public TrackValidator()
    {
        // Required fields
        RuleFor(t => t.Name).NotNull();
        RuleFor(t => t.Composer).NotNull();
        RuleFor(t => t.AlbumId).NotNull();
        RuleFor(t => t.GenreId).NotNull();
        RuleFor(t => t.MediaTypeId).NotNull();

        // Length constraints
        RuleFor(t => t.Name).MaximumLength(200);
        RuleFor(t => t.Composer).MaximumLength(220);

        // Business rules
        RuleFor(t => t.Bytes).GreaterThan(0);
        RuleFor(t => t.Milliseconds).GreaterThan(0);
        RuleFor(t => t.UnitPrice).GreaterThan(0);
        RuleFor(t => t.UnitPrice).LessThanOrEqualTo((decimal)9.99);  // Max price rule
    }
}
```

### Invoice Validator (with Address Validation)

```csharp
public class InvoiceValidator : AbstractValidator<InvoiceApiModel>
{
    public InvoiceValidator()
    {
        // Required references
        RuleFor(i => i.CustomerId).NotNull();
        RuleFor(i => i.InvoiceDate).NotNull();

        // Financial validation
        RuleFor(i => i.Total).NotNull();
        RuleFor(i => i.Total).GreaterThan(0);

        // Billing address required fields
        RuleFor(i => i.BillingAddress).NotNull();
        RuleFor(i => i.BillingCity).NotNull();
        RuleFor(i => i.BillingCountry).NotNull();
        RuleFor(i => i.BillingState).NotNull();
        RuleFor(i => i.BillingPostalCode).NotNull();

        // Length constraints
        RuleFor(i => i.BillingAddress).MaximumLength(70);
        RuleFor(i => i.BillingCity).MaximumLength(40);
        RuleFor(i => i.BillingCountry).MaximumLength(40);
        RuleFor(i => i.BillingState).MaximumLength(40);

        // Postal code format
        RuleFor(i => i.BillingPostalCode).Matches(@"^[0-9]{5}(?:-[0-9]{4})?$");
    }
}
```

---

## 5) Validation Rules Reference

### Common Rule Types Used

| Rule | Description | Example |
|------|-------------|---------|
| `NotNull()` | Field must not be null | `RuleFor(x => x.Name).NotNull()` |
| `NotEmpty()` | Field must not be null or empty string | `RuleFor(x => x.Name).NotEmpty()` |
| `MaximumLength(n)` | String max length | `RuleFor(x => x.Name).MaximumLength(120)` |
| `GreaterThan(n)` | Numeric must be greater than n | `RuleFor(x => x.Price).GreaterThan(0)` |
| `LessThanOrEqualTo(n)` | Numeric must be <= n | `RuleFor(x => x.Price).LessThanOrEqualTo(9.99m)` |
| `EmailAddress()` | Must be valid email format | `RuleFor(x => x.Email).EmailAddress()` |
| `Matches(regex)` | Must match regex pattern | `RuleFor(x => x.Phone).Matches(@"\d{3}-\d{4}")` |

### Regex Patterns Used

| Pattern | Purpose | Example Match |
|---------|---------|---------------|
| `\(?\d{3}\)?[-\.]? *\d{3}[-\.]? *[-\.]?\d{4}` | US phone number | `(555) 123-4567`, `555.123.4567` |
| `^[0-9]{5}(?:-[0-9]{4})?$` | US postal code | `12345`, `12345-6789` |

### Field Length Constraints

| Entity | Field | Max Length |
|--------|-------|------------|
| Customer | FirstName | 40 |
| Customer | LastName | 20 |
| Customer | Company | 80 |
| Customer | Address | 70 |
| Customer | City/State/Country | 40 |
| Artist | Name | 120 |
| Genre | Name | 120 |
| MediaType | Name | 120 |
| Track | Name | 200 |
| Track | Composer | 220 |
| Invoice | BillingAddress | 70 |
| Invoice | BillingCity/State/Country | 40 |

---

## 6) Validator Registration

Each module registers its own validators in `RegisterServices`; the host registers none:

```csharp
// Catalog.Module/Module.cs
services.AddValidatorsFromAssemblyContaining<AlbumValidator>(includeInternalTypes: true);

// Admin.Module/Module.cs: also adapts its FluentValidation validators for ValidationFilter<T>
services.AddValidatorsFromAssemblyContaining<CustomerValidator>(includeInternalTypes: true);
services.AddScoped(typeof(IRequestValidator<>), typeof(FluentRequestValidator<>));
```

Validators are `internal`, hence `includeInternalTypes: true`. Identity registers its hand-written
`AuthRequestValidators` as `IRequestValidator<LoginRequest>`, `<RefreshRequest>` and `<LogoutRequest>`.

---

## 7) Service Integration

### Validator Injection

Services receive validators via constructor injection:

```csharp
internal sealed class CustomerService(
    AdministrationDbContext db,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<CustomerApiModel> validator  // Injected validator
) : ICustomerService
{
    private readonly IValidator<CustomerApiModel> _validator = validator;
    // ...
}
```

### Validation Execution

Validation is performed before any persistence operation:

```csharp
public async Task<CustomerApiModel?> CreateCustomerAsync(
    CustomerApiModel model,
    CancellationToken ct)
{
    // 1. Execute validation
    var result = await _validator.ValidateAsync(model, ct);

    // 2. Check for validation failures
    if (!result.IsValid)
    {
        // 3. Throw ValidationException with all errors
        throw new ValidationException(result.Errors);
    }

    // 4. Proceed with persistence only if valid
    var entity = model.ToEntity();
    db.Customers.Add(entity);
    await db.SaveChangesAsync(ct);

    return entity.ToApiModel();
}
```

---

## 8) Error Response Handling

### Before the handler: `ValidationFilter<T>`

```csharp
// Admin.Module/Endpoints/GenreEndpoints.cs
group.MapPost("/genres", GenreHandlers.CreateGenre)
    .AddEndpointFilter<ValidationFilter<CreateGenreRequest>>()
    .RequireAdministrationWriteAccess();
```

The filter resolves `IRequestValidator<CreateGenreRequest>`, and on failure returns
`TypedResults.ValidationProblem(...)` with the title, detail and type constants it defines, plus `traceId`.

### After the handler: the host's exception handler

Handlers do not catch `ValidationException`. The host's exception handler
(`Program.WriteProblemDetailsResponseAsync`) maps it to the same body, so a service-level failure looks
exactly like a filter rejection. `EndpointFilterTests` checks the shape once for both paths.

### Example Error Response

```json
{
    "type": "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
    "title": "Request validation failed.",
    "status": 400,
    "detail": "One or more validation errors occurred.",
    "errors": {
        "Name": ["'Name' must not be empty."]
    },
    "traceId": "00-..."
}
```

---

## 9) Validation Patterns

### Pattern 1: Required Field

```csharp
RuleFor(x => x.Name).NotNull();
// Or with custom message
RuleFor(x => x.Name).NotNull().WithMessage("Name is required");
```

### Pattern 2: String Length

```csharp
RuleFor(x => x.Name)
    .NotNull()
    .MaximumLength(120)
    .WithMessage("Name must not exceed 120 characters");
```

### Pattern 3: Range Validation

```csharp
RuleFor(x => x.Price)
    .GreaterThan(0)
    .LessThanOrEqualTo(9.99m)
    .WithMessage("Price must be between $0.01 and $9.99");
```

### Pattern 4: Regex Validation

```csharp
RuleFor(x => x.PostalCode)
    .Matches(@"^[0-9]{5}(?:-[0-9]{4})?$")
    .WithMessage("Postal code must be in format 12345 or 12345-6789");
```

### Pattern 5: Conditional Validation

```csharp
RuleFor(x => x.State)
    .NotEmpty()
    .When(x => x.Country == "USA")
    .WithMessage("State is required for US addresses");
```

### Pattern 6: Custom Validation

```csharp
RuleFor(x => x.OrderDate)
    .Must(date => date <= DateTime.UtcNow)
    .WithMessage("Order date cannot be in the future");
```

---

## 10) Best Practices

### DO

1. **Add `ValidationFilter<T>` to write endpoints with a body** - and keep the service-level check as the fallback
2. **Use async validation** - `ValidateAsync` for consistency
3. **Include all errors** - Don't short-circuit; return all validation errors
4. **Use descriptive messages** - Help users understand what's wrong
5. **Validate business rules** - Price ranges, date constraints, etc.
6. **Keep validators focused** - One validator per model

### DON'T

1. **Don't write a second set of rules** - the filter and the service use the same validator
2. **Don't use DataAnnotations** - Stick to FluentValidation for consistency
3. **Don't catch ValidationException in services or handlers** - let it reach the host's exception handler
4. **Don't validate against another module's data** - a module validates only what it owns
5. **Don't mix validation with business logic** - Keep validators pure

---

## 11) Testing Validators

### Unit Testing a Validator

```csharp
public class CustomerValidatorTests
{
    private readonly CustomerValidator _validator = new();

    [Fact]
    public async Task Should_HaveError_When_FirstNameIsNull()
    {
        // Arrange
        var model = new CustomerApiModel { FirstName = null, LastName = "Doe" };

        // Act
        var result = await _validator.ValidateAsync(model);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public async Task Should_HaveError_When_EmailIsInvalid()
    {
        // Arrange
        var model = new CustomerApiModel
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "not-an-email"
        };

        // Act
        var result = await _validator.ValidateAsync(model);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task Should_BeValid_When_AllFieldsAreCorrect()
    {
        // Arrange
        var model = new CustomerApiModel
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "(555) 123-4567",
            PostalCode = "12345"
        };

        // Act
        var result = await _validator.ValidateAsync(model);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
```

---

## 12) Configuration

### FluentValidation Package Reference

In each module's project (`Catalog.Module`, `Orders.Module`, `Admin.Module`):

```xml
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
```

### Service Registration

In each module's `RegisterServices`:

```csharp
// Register the module's validators; they are internal, so include internal types
services.AddValidatorsFromAssemblyContaining<CustomerValidator>(includeInternalTypes: true);

// Or register individually
services.AddScoped<IValidator<CustomerApiModel>, CustomerValidator>();
```

---

## 13) Future Considerations

- **Async Database Validation** - Add `MustAsync` rules for uniqueness checks
- **Composite Validators** - Reusable address validators included in other validators
- **Localization** - Multi-language error messages
- **Severity Levels** - Warning vs Error for soft validation
- **Validator Caching** - Cache compiled validators for performance
- **OpenAPI Integration** - Generate validation schemas for Swagger

---

## 14) Related Documentation

- [Services Architecture](services-architecture.md) - Service layer implementation
- [Caching Strategy](caching-strategy.md) - Cache patterns (validation runs before cache lookup for writes)
- [OWASP Threats](owasp-top-threats-and-mitigations.md) - Input validation security
