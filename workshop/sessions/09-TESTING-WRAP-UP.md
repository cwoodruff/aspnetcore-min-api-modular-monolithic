# Session 9: Testing & Wrap-Up

**Duration:** 30 minutes
**Session Time:** 4:30 PM - 5:00 PM

---

## Overview

This final session covers integration testing with WebApplicationFactory,
testing authenticated endpoints, and reviews the key concepts learned throughout
the workshop.

---

## Learning Objectives

By the end of this session, you will:

- Write integration tests using WebApplicationFactory
- Test endpoints with authentication
- Mock user stores for testing
- Review all workshop concepts
- Know where to go for more resources

---

## Part 1: Integration Testing Setup (10 minutes)

### 1.1 Test Project Structure

```
tests/
└── ModularMonolith.Api.Tests/
    ├── ModularMonolith.Api.Tests.csproj
    ├── AlbumEndpointsTests.cs
    ├── ArtistEndpointsTests.cs
    ├── HealthEndpointsTests.cs
    ├── IdentityEndpointsTests.cs
    ├── TestAuthHelpers.cs
    └── Repositories/
        └── AlbumRepositoryTests.cs
```

### 1.2 Test Project References

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.x" />
    <PackageReference Include="xunit" Version="2.x" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.x" />
    <PackageReference Include="FluentAssertions" Version="7.x" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.x" />
</ItemGroup>

<ItemGroup>
    <ProjectReference Include="..\..\src\ModularMonolith.Api\ModularMonolith.Api.csproj" />
</ItemGroup>
```

### 1.3 Program.cs Partial Class

**File: `src/ModularMonolith.Api/Program.cs` (bottom)**

```csharp
// For WebApplicationFactory
#pragma warning disable ASP0027
namespace ModularMonolith.Api
{
    public partial class Program { }
}
#pragma warning restore ASP0027
```

This enables WebApplicationFactory to discover the entry point.

---

## Part 2: Test Helper Classes (10 minutes)

### 2.1 Test Auth Helpers

**File: `tests/ModularMonolith.Api.Tests/TestAuthHelpers.cs`**

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Identity.Modules.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Api.Tests;

public static class TestAuthHelpers
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public sealed record LoginResponse(string access_token, string token_type, DateTimeOffset expires_at_utc, string refresh_token);

    public static WebApplicationFactory<Program> WithTenantUser(this WebApplicationFactory<Program> factory, string tenantId = "tenant-123", string[]? permissions = null, string[]? roles = null)
    {
        permissions ??= new[] { "music.read", "orders.read", "administration.read" };
        roles ??= new[] { "User" };

        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUserStore));
                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddSingleton<IUserStore>(new TenantUserStore(tenantId, roles, permissions));
            });
        });
    }

    private sealed class TenantUserStore(string tenantId, string[] roles, string[] permissions) : IUserStore
    {
        public Task<(bool success, string userId, string? displayName, string[] roles, string[] permissions, string? email, string? tenant)> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
        {
            if (string.Equals(username, "demo", StringComparison.OrdinalIgnoreCase) && password == "<configured-demo-password>")
            {
                return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>((true, "user-1", "Demo User", roles, permissions, "demo@example.com", tenantId));
            }

            return Task.FromResult<(bool, string, string?, string[], string[], string?, string?)>((false, string.Empty, null, Array.Empty<string>(), Array.Empty<string>(), null, null));
        }
    }

    public static async Task<string> GetAccessTokenAsync(HttpClient client, string username = "demo", string password = "<configured-demo-password>")
    {
        var payload = JsonSerializer.Serialize(new { username, password });
        var resp = await client.PostAsync("/api/identity/login", new StringContent(payload, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync();
        var login = await JsonSerializer.DeserializeAsync<LoginResponse>(stream, JsonOpts);
        if (login is null || string.IsNullOrWhiteSpace(login.access_token))
            throw new InvalidOperationException("Failed to acquire access token for test user.");
        return login.access_token;
    }

    public static void UseBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
```

---

## Part 3: Integration Test Examples (10 minutes)

### 3.1 Album Endpoints Tests

**File: `tests/ModularMonolith.Api.Tests/AlbumEndpointsTests.cs`**

```csharp
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

public class AlbumEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task GetAlbumByIdShouldReturn401WhenNoTokenProvided()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/music/albums/1");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAlbumByIdShouldReturn200AndAlbumShapeWhenAuthorized()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);

        client.UseBearer(token);

        var response = await client.GetAsync("/api/music/albums/1");
        // Authorized calls should not be 401/403
        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Forbidden);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            root.TryGetProperty("id", out var idProp).Should().BeTrue();
            idProp.GetInt32().Should().BeGreaterThan(0);
            root.TryGetProperty("title", out var titleProp).Should().BeTrue();
            titleProp.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task GetAlbumByIdShouldReturn404WhenAuthorizedButNotFound()
    {
        var tenantFactory = _factory.WithTenantUser();
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);

        var response = await client.GetAsync("/api/music/albums/999999");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlbumByIdShouldReturn403WhenTenantHeaderProvidedButUserHasNoTenantClaim()
    {
        var tenantFactory = _factory.WithTenantUser("tenant-user");
        var client = tenantFactory.CreateClient();
        var token = await TestAuthHelpers.GetAccessTokenAsync(client);
        client.UseBearer(token);
        // Mismatch tenant between user (tenant-user) and request (tenant-other)
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-other");

        var response = await client.GetAsync("/api/music/albums/1");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }
}
```

### 3.2 Health Endpoints Tests

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ModularMonolith.Api.Tests;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/music/health")]
    [InlineData("/api/admin/health")]
    [InlineData("/api/orders/health")]
    [InlineData("/api/reporting/health")]
    [InlineData("/api/identity/health")]
    public async Task HealthEndpoints_ReturnOk(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
```

### 3.3 Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~AlbumEndpointsTests"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

---

## Workshop Summary

### Key Concepts Learned

| Session              | Key Takeaway                                |
|----------------------|---------------------------------------------|
| **1. Setup**         | Solution structure, IModule contract        |
| **2. Architecture**  | Modular Monolith benefits, host composition |
| **3. First Module**  | Health endpoints, endpoint metadata         |
| **4. Auth**          | JWT tokens, policies, tenant scoping        |
| **5. Repository**    | Base repository, EF Core patterns           |
| **6. Service/Cache** | Cache-aside, tag invalidation               |
| **7. Validation**    | FluentValidation, problem details           |
| **8. Rate Limiting** | Fixed window, partition keys                |
| **9. Testing**       | WebApplicationFactory, auth testing         |

### Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                      Minimal API Endpoints                   │
│  (RequireAuthorization, RequireRateLimiting, Produces)      │
├─────────────────────────────────────────────────────────────┤
│                      Service Layer                           │
│  (Caching, Validation, Business Logic)                      │
├─────────────────────────────────────────────────────────────┤
│                      Repository Layer                        │
│  (Data Access, EF Core, LINQ)                               │
├─────────────────────────────────────────────────────────────┤
│                      Shared Kernel                           │
│  (Entities, ApiModels, Infrastructure)                      │
└─────────────────────────────────────────────────────────────┘
```

### Module Structure

```
/Modules/YourModule/
├── Module.cs                 # IModule implementation
├── Endpoints/                # API routes
│   ├── HealthEndpoints.cs
│   ├── DataHealthEndpoints.cs
│   └── EntityEndpoints.cs
└── Services/                 # Business logic
    ├── IEntityService.cs
    └── EntityService.cs
```

---

## Next Steps

### 1. Production Readiness

- Add structured logging (Serilog)
- Add health checks (AspNetCore.HealthChecks)
- Add OpenTelemetry for observability
- Configure Redis for distributed caching

### 2. Explore Advanced Topics

See `/workshop/advanced-topics/` for:

- Endpoint Filters
- Output Caching
- API Versioning
- Background Services
- CQRS with MediatR
- SignalR real-time
- gRPC services

### 3. Resources

| Resource           | URL                                                              |
|--------------------|------------------------------------------------------------------|
| ASP.NET Core Docs  | https://docs.microsoft.com/aspnet/core                           |
| Minimal APIs Guide | https://docs.microsoft.com/aspnet/core/fundamentals/minimal-apis |
| FluentValidation   | https://docs.fluentvalidation.net                                |
| Rate Limiting      | https://docs.microsoft.com/aspnet/core/performance/rate-limit    |

---

## Q&A

Common questions addressed during the workshop:

**Q: When should I extract to microservices?**
A: When modules need independent scaling, deployment, or technology choices.

**Q: How do modules communicate?**
A: Currently through shared services. For eventual extraction, consider
event-driven patterns.

**Q: How do I add Redis caching?**
A: Configure `Caching:Tier` to `L1L2` and `Caching:Provider` to `Redis`, add
Redis connection string.

**Q: How do I add custom authorization policies?**
A: Add to `PolicyRegistry.Register()` in the Identity module.

---

## Feedback

Please provide feedback on this workshop:

- What worked well?
- What could be improved?
- What additional topics would you like covered?

Thank you for attending!

---

## Quick Test Reference

### Test Patterns

```csharp
// Anonymous endpoint test
[Fact]
public async Task HealthEndpoint_ReturnsOk()
{
    var client = _factory.CreateClient();
    var response = await client.GetAsync("/api/health");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

// Authenticated endpoint test
[Fact]
public async Task ProtectedEndpoint_WithToken_Succeeds()
{
    var tenantFactory = _factory.WithTenantUser();
    var client = tenantFactory.CreateClient();
    var token = await TestAuthHelpers.GetAccessTokenAsync(client);
    client.UseBearer(token);

    var response = await client.GetAsync("/api/protected");
    response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
}

// Unauthenticated test
[Fact]
public async Task ProtectedEndpoint_WithoutToken_Returns401()
{
    var client = _factory.CreateClient();
    var response = await client.GetAsync("/api/protected");
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

### Running Tests

```bash
dotnet test                              # All tests
dotnet test --filter "Category=Unit"     # By category
dotnet test --filter "ClassName~Album"   # By name
dotnet test --verbosity detailed         # Verbose
```
