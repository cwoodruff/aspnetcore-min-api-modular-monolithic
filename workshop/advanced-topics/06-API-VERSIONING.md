# API Versioning Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

API versioning allows you to evolve your API while maintaining backward compatibility for existing clients. ASP.NET Core provides flexible versioning strategies including URL path, query string, and header-based versioning.

**Duration:** 30-45 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of REST API design

---

## Learning Objectives

By the end of this guide, you will:
- Understand when and why to version APIs
- Implement URL path-based versioning
- Implement query string and header versioning
- Handle version deprecation
- Document versioned APIs in OpenAPI

---

## 1. When to Version Your API

### Version When You Have Breaking Changes

| Change Type | Breaking? | Requires New Version? |
|------------|-----------|----------------------|
| Add new endpoint | No | No |
| Add optional field to response | No | No |
| Add optional parameter | No | No |
| Remove endpoint | Yes | Yes |
| Remove response field | Yes | Yes |
| Rename field | Yes | Yes |
| Change field type | Yes | Yes |
| Change validation rules | Maybe | Consider |

### Versioning Strategies

| Strategy | Example | Pros | Cons |
|----------|---------|------|------|
| URL Path | `/api/v1/albums` | Clear, cacheable | Pollutes URLs |
| Query String | `/api/albums?api-version=1.0` | Non-invasive | Less discoverable |
| Header | `X-API-Version: 1.0` | Clean URLs | Hidden, harder to test |
| Media Type | `Accept: application/vnd.api.v1+json` | RESTful | Complex |

---

## 2. Setting Up API Versioning

### Install Package

```xml
<PackageReference Include="Asp.Versioning.Http" Version="8.0.0" />
<PackageReference Include="Asp.Versioning.Mvc.ApiExplorer" Version="8.0.0" />
```

### Basic Configuration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiVersioning(options =>
{
    // Default version when not specified
    options.DefaultApiVersion = new ApiVersion(1, 0);

    // Assume default version if not specified
    options.AssumeDefaultVersionWhenUnspecified = true;

    // Report available versions in response headers
    options.ReportApiVersions = true;

    // How to read version from request
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new QueryStringApiVersionReader("api-version"),
        new HeaderApiVersionReader("X-API-Version"));
})
.AddApiExplorer(options =>
{
    // Format: 'v'major[.minor][-status]
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

var app = builder.Build();
```

---

## 3. URL Path Versioning

### Define Versioned Endpoints

```csharp
// Program.cs
var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .HasApiVersion(new ApiVersion(2, 0))
    .ReportApiVersions()
    .Build();

// Version 1 endpoints
var v1 = app.MapGroup("/api/v{version:apiVersion}/albums")
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(new ApiVersion(1, 0));

v1.MapGet("/", GetAlbumsV1);
v1.MapGet("/{id}", GetAlbumByIdV1);
v1.MapPost("/", CreateAlbumV1);

// Version 2 endpoints
var v2 = app.MapGroup("/api/v{version:apiVersion}/albums")
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(new ApiVersion(2, 0));

v2.MapGet("/", GetAlbumsV2);
v2.MapGet("/{id}", GetAlbumByIdV2);
v2.MapPost("/", CreateAlbumV2);
```

### Version-Specific Handlers

```csharp
// V1 returns flat structure
static async Task<Ok<IEnumerable<AlbumV1>>> GetAlbumsV1(
    IAlbumService service,
    CancellationToken ct)
{
    var albums = await service.GetAllAsync(ct);
    var result = albums.Select(a => new AlbumV1
    {
        Id = a.Id,
        Title = a.Title,
        ArtistName = a.Artist?.Name ?? "Unknown"
    });
    return TypedResults.Ok(result);
}

// V2 returns richer structure with nested objects
static async Task<Ok<IEnumerable<AlbumV2>>> GetAlbumsV2(
    IAlbumService service,
    CancellationToken ct)
{
    var albums = await service.GetAllAsync(ct);
    var result = albums.Select(a => new AlbumV2
    {
        Id = a.Id,
        Title = a.Title,
        Artist = new ArtistSummary
        {
            Id = a.Artist?.ArtistId ?? 0,
            Name = a.Artist?.Name ?? "Unknown"
        },
        TrackCount = a.Tracks?.Count ?? 0,
        TotalDurationSeconds = a.Tracks?.Sum(t => t.Milliseconds / 1000) ?? 0
    });
    return TypedResults.Ok(result);
}

// DTOs
public record AlbumV1(int Id, string Title, string ArtistName);

public record AlbumV2(
    int Id,
    string Title,
    ArtistSummary Artist,
    int TrackCount,
    int TotalDurationSeconds);

public record ArtistSummary(int Id, string Name);
```

---

## 4. Query String Versioning

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new QueryStringApiVersionReader("api-version");
});

// Endpoints use same path
app.MapGet("/api/albums", GetAlbums)
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(new ApiVersion(1, 0));

app.MapGet("/api/albums", GetAlbumsV2)
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(new ApiVersion(2, 0));

// Requests:
// GET /api/albums?api-version=1.0
// GET /api/albums?api-version=2.0
```

---

## 5. Header Versioning

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new HeaderApiVersionReader("X-API-Version");
});

// Requests:
// GET /api/albums
// X-API-Version: 1.0
```

---

## 6. Multiple Version Readers

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;

    // Support all versioning methods
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),                    // /api/v1/albums
        new QueryStringApiVersionReader("api-version"),      // ?api-version=1.0
        new HeaderApiVersionReader("X-API-Version"),         // X-API-Version: 1.0
        new MediaTypeApiVersionReader("ver"));               // Accept: application/json;ver=1.0
});
```

---

## 7. Deprecating Versions

```csharp
var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .HasDeprecatedApiVersion(new ApiVersion(0, 9))  // Deprecated
    .HasApiVersion(new ApiVersion(2, 0))
    .Build();

// Or per-endpoint deprecation
app.MapGet("/api/v{version:apiVersion}/albums/legacy", GetAlbumsLegacy)
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(new ApiVersion(0, 9));
```

### Response Headers for Deprecated Versions

```http
HTTP/1.1 200 OK
api-supported-versions: 1.0, 2.0
api-deprecated-versions: 0.9
```

---

## 8. Version-Neutral Endpoints

Some endpoints (health checks, documentation) shouldn't be versioned:

```csharp
// Health check - version neutral
app.MapHealthChecks("/health")
    .WithApiVersionSet(versionSet)
    .IsApiVersionNeutral();

// Root endpoint - version neutral
app.MapGet("/", () => new { service = "ModularMonolith.Api", status = "running" })
    .IsApiVersionNeutral();
```

---

## 9. OpenAPI Documentation per Version

### Configure Swagger for Multiple Versions

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Modular Monolith API",
        Version = "v1",
        Description = "Version 1.0 - Original API"
    });

    options.SwaggerDoc("v2", new OpenApiInfo
    {
        Title = "Modular Monolith API",
        Version = "v2",
        Description = "Version 2.0 - Enhanced responses with nested objects"
    });
});

// Configure API explorer for versioning
builder.Services.AddApiVersioning()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
    options.SwaggerEndpoint("/swagger/v2/swagger.json", "API v2");
});
```

### Custom Version Description Provider

```csharp
// Swagger/ConfigureSwaggerOptions.cs
public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
        }
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = "Modular Monolith API",
            Version = description.ApiVersion.ToString(),
            Description = "ASP.NET Core Minimal API with Modular Monolith Architecture"
        };

        if (description.IsDeprecated)
        {
            info.Description += " **This API version has been deprecated.**";
        }

        return info;
    }
}

// Registration
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
```

---

## 10. Module-Based Versioning

### Versioned Module Interface

```csharp
// IVersionedModule.cs
public interface IVersionedModule : IModule
{
    IEnumerable<ApiVersion> SupportedVersions { get; }
    void MapEndpoints(IEndpointRouteBuilder endpoints, ApiVersion version);
}

// MusicModule.cs
public class MusicModule : IVersionedModule
{
    public string Name => "Music";

    public IEnumerable<ApiVersion> SupportedVersions => new[]
    {
        new ApiVersion(1, 0),
        new ApiVersion(2, 0)
    };

    public void RegisterServices(IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IAlbumService, AlbumService>();
        services.AddScoped<IArtistService, ArtistService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Map for all supported versions
        foreach (var version in SupportedVersions)
        {
            MapEndpoints(endpoints, version);
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, ApiVersion version)
    {
        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(version)
            .Build();

        var group = endpoints
            .MapGroup($"/api/v{{version:apiVersion}}/music")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(version)
            .WithTags("Music");

        if (version.MajorVersion == 1)
        {
            MapV1Endpoints(group);
        }
        else if (version.MajorVersion == 2)
        {
            MapV2Endpoints(group);
        }
    }

    private void MapV1Endpoints(RouteGroupBuilder group)
    {
        group.MapGet("/albums", AlbumEndpointsV1.GetAll);
        group.MapGet("/albums/{id}", AlbumEndpointsV1.GetById);
        // ...
    }

    private void MapV2Endpoints(RouteGroupBuilder group)
    {
        group.MapGet("/albums", AlbumEndpointsV2.GetAll);
        group.MapGet("/albums/{id}", AlbumEndpointsV2.GetById);
        // ...
    }
}
```

---

## 11. Version Migration Helpers

### Adapter Pattern for Backward Compatibility

```csharp
// Adapters/AlbumAdapter.cs
public static class AlbumAdapter
{
    public static AlbumV1 ToV1(AlbumModel album)
    {
        return new AlbumV1
        {
            Id = album.Id,
            Title = album.Title,
            ArtistName = album.Artist?.Name ?? "Unknown"
        };
    }

    public static AlbumV2 ToV2(AlbumModel album)
    {
        return new AlbumV2
        {
            Id = album.Id,
            Title = album.Title,
            Artist = album.Artist is not null
                ? new ArtistSummary(album.Artist.ArtistId, album.Artist.Name)
                : null,
            TrackCount = album.Tracks?.Count ?? 0,
            TotalDuration = TimeSpan.FromMilliseconds(
                album.Tracks?.Sum(t => t.Milliseconds) ?? 0),
            CreatedAt = album.CreatedAt,
            UpdatedAt = album.UpdatedAt
        };
    }

    public static AlbumCreateRequest FromV1(AlbumCreateRequestV1 request)
    {
        return new AlbumCreateRequest
        {
            Title = request.Title,
            ArtistId = request.ArtistId
        };
    }

    public static AlbumCreateRequest FromV2(AlbumCreateRequestV2 request)
    {
        return new AlbumCreateRequest
        {
            Title = request.Title,
            ArtistId = request.Artist.Id,
            GenreId = request.GenreId,
            ReleaseDate = request.ReleaseDate
        };
    }
}
```

### Shared Service Layer

```csharp
// The service layer remains version-agnostic
public interface IAlbumService
{
    Task<AlbumModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<AlbumModel>> GetAllAsync(CancellationToken ct = default);
    Task<AlbumModel> CreateAsync(AlbumCreateRequest request, CancellationToken ct = default);
}

// Endpoints handle version-specific transformations
static async Task<Results<Ok<AlbumV1>, NotFound>> GetAlbumByIdV1(
    int id,
    IAlbumService service,
    CancellationToken ct)
{
    var album = await service.GetByIdAsync(id, ct);
    return album is not null
        ? TypedResults.Ok(AlbumAdapter.ToV1(album))
        : TypedResults.NotFound();
}

static async Task<Results<Ok<AlbumV2>, NotFound>> GetAlbumByIdV2(
    int id,
    IAlbumService service,
    CancellationToken ct)
{
    var album = await service.GetByIdAsync(id, ct);
    return album is not null
        ? TypedResults.Ok(AlbumAdapter.ToV2(album))
        : TypedResults.NotFound();
}
```

---

## 12. Testing Versioned APIs

```csharp
public class VersionedApiTests
{
    [Fact]
    public async Task GetAlbums_V1_ReturnsFlattStructure()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/albums");

        response.EnsureSuccessStatusCode();
        var albums = await response.Content.ReadFromJsonAsync<IEnumerable<AlbumV1>>();

        Assert.NotNull(albums);
        Assert.All(albums, a =>
        {
            Assert.NotNull(a.Title);
            Assert.NotNull(a.ArtistName); // V1 has flat artist name
        });
    }

    [Fact]
    public async Task GetAlbums_V2_ReturnsNestedStructure()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/v2/albums");

        response.EnsureSuccessStatusCode();
        var albums = await response.Content.ReadFromJsonAsync<IEnumerable<AlbumV2>>();

        Assert.NotNull(albums);
        Assert.All(albums, a =>
        {
            Assert.NotNull(a.Title);
            Assert.NotNull(a.Artist); // V2 has nested artist object
            Assert.True(a.TrackCount >= 0);
        });
    }

    [Fact]
    public async Task GetAlbums_WithQueryStringVersion_ReturnsCorrectVersion()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/albums?api-version=2.0");

        response.EnsureSuccessStatusCode();
        // Should return V2 format
    }

    [Fact]
    public async Task GetAlbums_WithHeaderVersion_ReturnsCorrectVersion()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/albums");
        request.Headers.Add("X-API-Version", "2.0");

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        // Should return V2 format
    }

    [Fact]
    public async Task GetAlbums_DeprecatedVersion_ReturnsDeprecationHeader()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/v0.9/albums");

        Assert.Contains("0.9",
            response.Headers.GetValues("api-deprecated-versions"));
    }
}
```

---

## Summary

### Versioning Quick Reference

| Strategy | URL Example | Configuration |
|----------|-------------|---------------|
| URL Path | `/api/v1/albums` | `UrlSegmentApiVersionReader()` |
| Query String | `/api/albums?api-version=1.0` | `QueryStringApiVersionReader("api-version")` |
| Header | `X-API-Version: 1.0` | `HeaderApiVersionReader("X-API-Version")` |

### Best Practices

1. **Start with URL path versioning** — Most explicit and cacheable
2. **Support multiple readers** — Flexibility for different clients
3. **Deprecate before removing** — Give clients time to migrate
4. **Document version differences** — Clear changelog in OpenAPI docs
5. **Keep service layer version-agnostic** — Transform at endpoint level
6. **Test all supported versions** — Ensure backward compatibility

### Files to Create

```
src/
├── Modules/
│   └── Music/
│       └── Music.Module/
│           ├── Endpoints/
│           │   ├── V1/
│           │   │   └── AlbumEndpointsV1.cs
│           │   └── V2/
│           │       └── AlbumEndpointsV2.cs
│           └── Adapters/
│               └── AlbumAdapter.cs
└── Shared/
    └── SharedKernel.Persistence/
        └── ApiModels/
            ├── V1/
            │   └── AlbumV1.cs
            └── V2/
                └── AlbumV2.cs
```
