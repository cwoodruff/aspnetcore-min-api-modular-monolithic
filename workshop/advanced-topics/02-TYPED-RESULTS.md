# Typed Results Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

Typed Results provide compile-time safety and automatic OpenAPI documentation
for your Minimal API endpoints. Instead of returning generic `IResult`, you
declare exactly what types your endpoint can return.

**Duration:** 30-45 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of OpenAPI/Swagger

---

## Learning Objectives

By the end of this guide, you will:

- Understand the difference between `IResult` and `Results<T1, T2, ...>`
- Implement typed results for accurate OpenAPI schemas
- Use `TypedResults` factory methods
- Handle multiple response types elegantly
- Generate better client code from OpenAPI specs

---

## 1. The Problem with Untyped Results

### Traditional Approach (Untyped)

```csharp
// OpenAPI doesn't know what this returns
app.MapGet("/albums/{id}", async (int id, IAlbumService service) =>
{
    var album = await service.GetByIdAsync(id);
    return album is not null ? Results.Ok(album) : Results.NotFound();
});
```

**Issues:**

- OpenAPI shows generic response types
- No compile-time validation of return types
- Client code generators can't create proper types
- Must manually add `.Produces<T>()` annotations

### Typed Results Approach

```csharp
// OpenAPI automatically documents all return types
app.MapGet("/albums/{id}", async Task<Results<Ok<AlbumModel>, NotFound>>
    (int id, IAlbumService service) =>
{
    var album = await service.GetByIdAsync(id);
    return album is not null
        ? TypedResults.Ok(album)
        : TypedResults.NotFound();
});
```

**Benefits:**

- Automatic OpenAPI documentation
- Compile-time type checking
- Self-documenting code
- Better IDE support

---

## 2. TypedResults Factory Methods

### Common TypedResults Methods

```csharp
// Success responses
TypedResults.Ok()                           // 200 OK (no body)
TypedResults.Ok(value)                      // 200 OK with body
TypedResults.Created(uri, value)            // 201 Created
TypedResults.CreatedAtRoute(routeName, routeValues, value)
TypedResults.Accepted(uri, value)           // 202 Accepted
TypedResults.NoContent()                    // 204 No Content

// Client error responses
TypedResults.BadRequest()                   // 400 Bad Request
TypedResults.BadRequest(error)              // 400 with error object
TypedResults.ValidationProblem(errors)     // 400 with validation errors
TypedResults.Unauthorized()                 // 401 Unauthorized
TypedResults.Forbid()                       // 403 Forbidden
TypedResults.NotFound()                     // 404 Not Found
TypedResults.NotFound(value)                // 404 with body
TypedResults.Conflict()                     // 409 Conflict
TypedResults.Conflict(value)                // 409 with body
TypedResults.UnprocessableEntity()          // 422 Unprocessable Entity

// Server error responses
TypedResults.Problem(problemDetails)        // 500 or custom status
TypedResults.StatusCode(statusCode)         // Any status code

// Redirects
TypedResults.Redirect(url)                  // 302 Found
TypedResults.RedirectPermanent(url)         // 301 Moved Permanently
TypedResults.RedirectToRoute(routeName)     // Redirect to named route

// Content
TypedResults.Json(value)                    // JSON response
TypedResults.Text(content)                  // Plain text
TypedResults.File(bytes, contentType)       // File download
TypedResults.Stream(stream, contentType)    // Stream response
```

---

## 3. Results Union Types

### Two Return Types

```csharp
app.MapGet("/albums/{id}", async Task<Results<Ok<AlbumModel>, NotFound>>
    (int id, IAlbumService service) =>
{
    var album = await service.GetByIdAsync(id);
    return album is not null
        ? TypedResults.Ok(album)
        : TypedResults.NotFound();
});
```

### Three Return Types

```csharp
app.MapPost("/albums", async Task<Results<Created<AlbumModel>, BadRequest<ProblemDetails>, Conflict>>
    (AlbumCreateRequest request, IAlbumService service) =>
{
    // Validation failed
    if (string.IsNullOrEmpty(request.Title))
    {
        return TypedResults.BadRequest(new ProblemDetails
        {
            Title = "Validation Error",
            Detail = "Title is required"
        });
    }

    // Check for duplicate
    if (await service.ExistsAsync(request.Title))
    {
        return TypedResults.Conflict();
    }

    // Create album
    var album = await service.CreateAsync(request);
    return TypedResults.Created($"/albums/{album.Id}", album);
});
```

### Four Return Types (Maximum without custom)

```csharp
app.MapPut("/albums/{id}",
    async Task<Results<Ok<AlbumModel>, NotFound, BadRequest<ValidationProblemDetails>, Conflict<ProblemDetails>>>
    (int id, AlbumUpdateRequest request, IAlbumService service, IValidator<AlbumUpdateRequest> validator) =>
{
    // Validate
    var validation = await validator.ValidateAsync(request);
    if (!validation.IsValid)
    {
        return TypedResults.BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
    }

    // Check exists
    var existing = await service.GetByIdAsync(id);
    if (existing is null)
    {
        return TypedResults.NotFound();
    }

    // Check for title conflict
    if (await service.TitleExistsAsync(request.Title, excludeId: id))
    {
        return TypedResults.Conflict(new ProblemDetails
        {
            Title = "Conflict",
            Detail = $"An album with title '{request.Title}' already exists"
        });
    }

    // Update
    var updated = await service.UpdateAsync(id, request);
    return TypedResults.Ok(updated);
});
```

### Five or More Return Types (Custom Union)

```csharp
// For more than 4 types, create a custom Results type
// Or use Results<T1, Results<T2, Results<T3, T4>>> nesting (not recommended)

// Better: Create specific result types
public class AlbumOperationResult
{
    public static Results<Ok<AlbumModel>, NotFound, BadRequest<ProblemDetails>,
        Conflict<ProblemDetails>, UnprocessableEntity<ProblemDetails>>
        Success(AlbumModel album) => TypedResults.Ok(album);

    public static Results<Ok<AlbumModel>, NotFound, BadRequest<ProblemDetails>,
        Conflict<ProblemDetails>, UnprocessableEntity<ProblemDetails>>
        NotFound() => TypedResults.NotFound();

    // ... etc
}
```

---

## 4. Complete CRUD Example with Typed Results

```csharp
// Endpoints/AlbumEndpoints.cs
public static class AlbumEndpoints
{
    public static void MapAlbumEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/albums")
            .WithTags("Albums");

        group.MapGet("/", GetAllAlbums)
            .WithName("GetAllAlbums")
            .WithSummary("Get all albums with optional filtering");

        group.MapGet("/{id:int}", GetAlbumById)
            .WithName("GetAlbumById")
            .WithSummary("Get a specific album by ID");

        group.MapPost("/", CreateAlbum)
            .WithName("CreateAlbum")
            .WithSummary("Create a new album");

        group.MapPut("/{id:int}", UpdateAlbum)
            .WithName("UpdateAlbum")
            .WithSummary("Update an existing album");

        group.MapDelete("/{id:int}", DeleteAlbum)
            .WithName("DeleteAlbum")
            .WithSummary("Delete an album");
    }

    // GET /api/albums
    private static async Task<Ok<IEnumerable<AlbumModel>>> GetAllAlbums(
        [AsParameters] AlbumQueryParameters query,
        IAlbumService service,
        CancellationToken ct)
    {
        var albums = await service.GetAllAsync(query, ct);
        return TypedResults.Ok(albums);
    }

    // GET /api/albums/{id}
    private static async Task<Results<Ok<AlbumModel>, NotFound>> GetAlbumById(
        int id,
        IAlbumService service,
        CancellationToken ct)
    {
        var album = await service.GetByIdAsync(id, ct);
        return album is not null
            ? TypedResults.Ok(album)
            : TypedResults.NotFound();
    }

    // POST /api/albums
    private static async Task<Results<Created<AlbumModel>, ValidationProblem>> CreateAlbum(
        AlbumCreateRequest request,
        IAlbumService service,
        IValidator<AlbumCreateRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var album = await service.CreateAsync(request, ct);
        return TypedResults.Created($"/api/albums/{album.Id}", album);
    }

    // PUT /api/albums/{id}
    private static async Task<Results<Ok<AlbumModel>, NotFound, ValidationProblem>> UpdateAlbum(
        int id,
        AlbumUpdateRequest request,
        IAlbumService service,
        IValidator<AlbumUpdateRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var album = await service.UpdateAsync(id, request, ct);
        return album is not null
            ? TypedResults.Ok(album)
            : TypedResults.NotFound();
    }

    // DELETE /api/albums/{id}
    private static async Task<Results<NoContent, NotFound>> DeleteAlbum(
        int id,
        IAlbumService service,
        CancellationToken ct)
    {
        var deleted = await service.DeleteAsync(id, ct);
        return deleted
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }
}
```

---

## 5. OpenAPI Enhancement with Descriptions

```csharp
// Add detailed descriptions for better documentation
app.MapGet("/albums/{id}", GetAlbumById)
    .WithName("GetAlbumById")
    .WithSummary("Retrieve a specific album")
    .WithDescription("Returns a single album by its unique identifier. " +
                     "Returns 404 if the album doesn't exist.")
    .WithOpenApi(operation =>
    {
        operation.Parameters[0].Description = "The unique album identifier";
        operation.Responses["200"].Description = "The album was found";
        operation.Responses["404"].Description = "No album exists with the specified ID";
        return operation;
    });

// Method signature provides automatic response documentation
private static async Task<Results<Ok<AlbumModel>, NotFound>> GetAlbumById(
    int id,
    IAlbumService service,
    CancellationToken ct)
{
    // ...
}
```

---

## 6. Problem Details Integration

```csharp
// Consistent error responses with RFC 7807 Problem Details
app.MapPost("/albums", async Task<Results<Created<AlbumModel>, ValidationProblem, ProblemHttpResult>>
    (AlbumCreateRequest request, IAlbumService service, IValidator<AlbumCreateRequest> validator) =>
{
    // Validation errors → ValidationProblem
    var validation = await validator.ValidateAsync(request);
    if (!validation.IsValid)
    {
        return TypedResults.ValidationProblem(validation.ToDictionary());
    }

    try
    {
        var album = await service.CreateAsync(request);
        return TypedResults.Created($"/albums/{album.Id}", album);
    }
    catch (DuplicateAlbumException ex)
    {
        // Business logic errors → Problem
        return TypedResults.Problem(
            title: "Duplicate Album",
            detail: ex.Message,
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?>
            {
                ["existingAlbumId"] = ex.ExistingAlbumId
            });
    }
});
```

---

## 7. Paginated Results

```csharp
// Models/PagedResult.cs
public record PagedResult<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

// Endpoint with pagination
app.MapGet("/albums", async Task<Ok<PagedResult<AlbumModel>>> (
    [AsParameters] PaginationParameters pagination,
    IAlbumService service,
    CancellationToken ct) =>
{
    var (items, totalCount) = await service.GetPagedAsync(
        pagination.Page,
        pagination.PageSize,
        ct);

    var result = new PagedResult<AlbumModel>(
        Items: items,
        Page: pagination.Page,
        PageSize: pagination.PageSize,
        TotalCount: totalCount,
        TotalPages: (int)Math.Ceiling(totalCount / (double)pagination.PageSize));

    return TypedResults.Ok(result);
});

// Parameter binding class
public record PaginationParameters(
    [FromQuery] int Page = 1,
    [FromQuery] int PageSize = 20);
```

---

## 8. File Downloads with Typed Results

```csharp
app.MapGet("/albums/{id}/cover",
    async Task<Results<FileContentHttpResult, NotFound>> (
        int id,
        IAlbumService service,
        CancellationToken ct) =>
{
    var coverImage = await service.GetCoverImageAsync(id, ct);

    if (coverImage is null)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.File(
        coverImage.Data,
        contentType: coverImage.ContentType,
        fileDownloadName: $"album-{id}-cover{coverImage.Extension}");
});
```

---

## 9. Streaming Results

```csharp
app.MapGet("/albums/export",
    async Task<Results<FileStreamHttpResult, ProblemHttpResult>> (
        IAlbumService service,
        CancellationToken ct) =>
{
    try
    {
        var stream = await service.ExportToCsvAsync(ct);

        return TypedResults.Stream(
            stream,
            contentType: "text/csv",
            fileDownloadName: $"albums-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }
    catch (Exception ex)
    {
        return TypedResults.Problem(
            title: "Export Failed",
            detail: ex.Message,
            statusCode: 500);
    }
});
```

---

## 10. Conditional Responses (ETag/Last-Modified)

```csharp
app.MapGet("/albums/{id}",
    async Task<Results<Ok<AlbumModel>, NotFound, StatusCodeHttpResult>> (
        int id,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        IAlbumService service,
        HttpContext httpContext,
        CancellationToken ct) =>
{
    var album = await service.GetByIdAsync(id, ct);

    if (album is null)
    {
        return TypedResults.NotFound();
    }

    var etag = $"\"{album.Version}\"";

    // Check if client has current version
    if (ifNoneMatch == etag)
    {
        return TypedResults.StatusCode(StatusCodes.Status304NotModified);
    }

    httpContext.Response.Headers.ETag = etag;
    httpContext.Response.Headers.LastModified = album.ModifiedAt.ToString("R");

    return TypedResults.Ok(album);
});
```

---

## 11. Generated OpenAPI Schema

With typed results, your OpenAPI spec automatically includes:

```yaml
paths:
  /api/albums/{id}:
    get:
      summary: Get a specific album by ID
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: integer
            format: int32
      responses:
        '200':
          description: Success
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/AlbumModel'
        '404':
          description: Not Found

  /api/albums:
    post:
      summary: Create a new album
      requestBody:
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/AlbumCreateRequest'
      responses:
        '201':
          description: Created
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/AlbumModel'
        '400':
          description: Validation Error
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ValidationProblemDetails'
```

---

## 12. Testing Typed Results

```csharp
public class AlbumEndpointsTests
{
    [Fact]
    public async Task GetAlbumById_ReturnsOk_WhenAlbumExists()
    {
        // Arrange
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/albums/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var album = await response.Content.ReadFromJsonAsync<AlbumModel>();
        Assert.NotNull(album);
        Assert.Equal(1, album.Id);
    }

    [Fact]
    public async Task GetAlbumById_ReturnsNotFound_WhenAlbumDoesNotExist()
    {
        // Arrange
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/albums/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAlbum_ReturnsValidationProblem_WhenInvalid()
    {
        // Arrange
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var invalidRequest = new AlbumCreateRequest { Title = "" };

        // Act
        var response = await client.PostAsJsonAsync("/api/albums", invalidRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }
}
```

---

## Summary

### Migration Checklist

1. Change return type from `IResult` to `Task<Results<T1, T2, ...>>`
2. Replace `Results.Ok()` with `TypedResults.Ok()`
3. Replace `Results.NotFound()` with `TypedResults.NotFound()`
4. Remove manual `.Produces<T>()` calls (now automatic)
5. Add `WithSummary()` and `WithDescription()` for documentation

### Best Practices

| Practice                                      | Reason                               |
|-----------------------------------------------|--------------------------------------|
| Use typed results for all public endpoints    | Better documentation and type safety |
| Keep return types to 4 or fewer               | Readability and maintainability      |
| Use `ValidationProblem` for validation errors | Consistent RFC 7807 format           |
| Use `ProblemHttpResult` for other errors      | Consistent error response format     |
| Add summaries and descriptions                | Better generated documentation       |

### Quick Reference

```csharp
// Common patterns
Task<Results<Ok<T>, NotFound>>                    // GET by ID
Task<Results<Created<T>, ValidationProblem>>      // POST
Task<Results<Ok<T>, NotFound, ValidationProblem>> // PUT
Task<Results<NoContent, NotFound>>                // DELETE
Task<Ok<IEnumerable<T>>>                          // GET list
Task<Ok<PagedResult<T>>>                          // GET paged
```
