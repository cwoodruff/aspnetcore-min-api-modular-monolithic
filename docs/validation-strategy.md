# Validation Strategy

**Status: Implemented**

This document describes the validation architecture implemented in the Modular Monolith using FluentValidation. All input validation is centralized in the SharedKernel.Persistence project and consumed by service layer classes.

---

## 1) Executive Summary

The solution uses **FluentValidation** for all input validation with the following design principles:

- **Centralized validators** in `SharedKernel.Persistence/Validation/`
- **Service-layer validation** - Validators are injected into services and executed before persistence
- **Consistent error responses** - ValidationException is caught by endpoints and converted to RFC 7807 ProblemDetails
- **Auto-discovery registration** - Validators are registered via assembly scanning

---

## 2) Architecture Overview

### Validation Flow

```
HTTP Request (JSON body)
    ↓
Endpoint (Model Binding)
    ↓
Service Method
    ↓
FluentValidation (IValidator<T>.ValidateAsync)
    ↓
    ├─ Valid: Continue to Repository
    └─ Invalid: Throw ValidationException
            ↓
        Endpoint catches exception
            ↓
        Results.ValidationProblem (HTTP 400)
```

### Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **Validators** | Define validation rules for API models |
| **Services** | Execute validation before persistence operations |
| **Endpoints** | Catch ValidationException and return HTTP 400 |
| **PersistenceRegistration** | Register validators via assembly scanning |

---

## 3) Validator Implementation

### Location

All validators are located in:
```
src/Shared/SharedKernel.Persistence/Validation/
```

### Validator Registry

| Validator | Model | Module |
|-----------|-------|--------|
| `CustomerValidator` | `CustomerApiModel` | Administration |
| `EmployeeValidator` | `EmployeeApiModel` | Administration |
| `GenreValidator` | `GenreApiModel` | Administration |
| `MediaTypeValidator` | `MediaTypeApiModel` | Administration |
| `ArtistValidator` | `ArtistApiModel` | Music |
| `AlbumValidator` | `AlbumApiModel` | Music |
| `TrackValidator` | `TrackApiModel` | Music |
| `PlaylistValidator` | `PlaylistApiModel` | Music |
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

Validators are registered automatically via assembly scanning in `PersistenceRegistration.cs`:

```csharp
public static class PersistenceRegistration
{
    public static IServiceCollection AddKernelPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ... DbContext registration ...

        // Register all validators from the assembly
        services.AddValidatorsFromAssemblyContaining<CustomerValidator>();

        return services;
    }
}
```

This single line registers all classes inheriting from `AbstractValidator<T>` in the assembly.

---

## 7) Service Integration

### Validator Injection

Services receive validators via constructor injection:

```csharp
public sealed class CustomerService(
    ICustomerRepository repo,
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
    var entity = model.Convert();
    var created = await repo.Add(entity);

    return created?.Convert();
}
```

---

## 8) Error Response Handling

### Endpoint Exception Handling

Endpoints catch `ValidationException` and convert to HTTP 400 with ProblemDetails:

```csharp
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
        // Convert to RFC 7807 ValidationProblem response
        return Results.ValidationProblem(
            ex.Errors.ToDictionary(
                e => e.PropertyName,
                e => new[] { e.ErrorMessage }));
    }
});
```

### Example Error Response

```json
{
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
    "title": "One or more validation errors occurred.",
    "status": 400,
    "errors": {
        "FirstName": ["'First Name' must not be empty."],
        "Email": ["'Email' is not a valid email address."],
        "Phone": ["'Phone' is not in the correct format."]
    }
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

1. **Validate at service layer** - Not at endpoint or repository level
2. **Use async validation** - `ValidateAsync` for consistency
3. **Include all errors** - Don't short-circuit; return all validation errors
4. **Use descriptive messages** - Help users understand what's wrong
5. **Validate business rules** - Price ranges, date constraints, etc.
6. **Keep validators focused** - One validator per model

### DON'T

1. **Don't validate at multiple layers** - Avoid duplication
2. **Don't use DataAnnotations** - Stick to FluentValidation for consistency
3. **Don't catch ValidationException in services** - Let it bubble to endpoints
4. **Don't validate in repositories** - Repositories handle persistence only
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

In `SharedKernel.Persistence.csproj`:

```xml
<PackageReference Include="FluentValidation" Version="11.x" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.x" />
```

### Service Registration

In host `Program.cs` or via `PersistenceRegistration`:

```csharp
// Auto-register all validators from assembly
services.AddValidatorsFromAssemblyContaining<CustomerValidator>();

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
