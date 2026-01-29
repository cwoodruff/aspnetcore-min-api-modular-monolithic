# Feature Flags Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

Feature flags enable you to toggle functionality on/off without deploying new
code. They're essential for continuous deployment, A/B testing, gradual
rollouts, and managing incomplete features in production.

**Duration:** 30-45 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of configuration

---

## Learning Objectives

By the end of this guide, you will:

- Understand feature flag patterns and use cases
- Implement Microsoft.FeatureManagement
- Create custom feature filters
- Use feature flags in endpoints and services
- Manage flags with different targeting strategies

---

## 1. Why Feature Flags?

### Use Cases

| Use Case                    | Description                                         |
|-----------------------------|-----------------------------------------------------|
| **Trunk-based development** | Merge incomplete features, toggle off in production |
| **Gradual rollout**         | Enable for 10%, then 50%, then 100% of users        |
| **A/B testing**             | Show different experiences to different user groups |
| **Kill switch**             | Instantly disable problematic features              |
| **Beta features**           | Enable for specific users or tenants                |
| **Operational toggles**     | Disable expensive features during high load         |

### Flag Types

```
Feature Flags
├── Release Flags      → Enable/disable features (temporary)
├── Experiment Flags   → A/B testing (temporary)
├── Ops Flags          → Control operational aspects (long-term)
└── Permission Flags   → Feature access control (long-term)
```

---

## 2. Setting Up Microsoft.FeatureManagement

### Install Packages

```xml
<PackageReference Include="Microsoft.FeatureManagement.AspNetCore" Version="3.2.0" />
```

### Basic Configuration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add feature management
builder.Services.AddFeatureManagement();

var app = builder.Build();
```

### appsettings.json Configuration

```json
{
  "FeatureManagement": {
    "NewSearchAlgorithm": true,
    "BetaFeatures": false,
    "PremiumRecommendations": true,
    "DarkMode": false,
    "ExperimentalApi": false
  }
}
```

---

## 3. Using Feature Flags in Endpoints

### Check Flag in Endpoint

```csharp
app.MapGet("/api/search", async (
    string query,
    IFeatureManager featureManager,
    ISearchService searchService,
    CancellationToken ct) =>
{
    if (await featureManager.IsEnabledAsync("NewSearchAlgorithm"))
    {
        return await searchService.SearchV2Async(query, ct);
    }

    return await searchService.SearchV1Async(query, ct);
});
```

### Feature Gate Attribute (Minimal API Extension)

```csharp
// Extensions/FeatureGateExtensions.cs
public static class FeatureGateExtensions
{
    public static RouteHandlerBuilder RequireFeature(
        this RouteHandlerBuilder builder,
        string featureName)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var featureManager = context.HttpContext.RequestServices
                .GetRequiredService<IFeatureManager>();

            if (!await featureManager.IsEnabledAsync(featureName))
            {
                return TypedResults.NotFound(new ProblemDetails
                {
                    Title = "Feature Not Available",
                    Detail = $"The feature '{featureName}' is not currently available.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return await next(context);
        });
    }
}

// Usage
app.MapGet("/api/experimental/recommendations", GetRecommendations)
    .RequireFeature("PremiumRecommendations");

app.MapPost("/api/beta/ai-search", AiSearch)
    .RequireFeature("BetaFeatures");
```

### Conditional Endpoint Registration

```csharp
// Only register endpoint if feature is enabled at startup
var featureManager = builder.Services.BuildServiceProvider()
    .GetRequiredService<IFeatureManager>();

if (await featureManager.IsEnabledAsync("ExperimentalApi"))
{
    app.MapGroup("/api/experimental")
        .MapExperimentalEndpoints();
}
```

---

## 4. Feature Filters

### Percentage Filter (Gradual Rollout)

```json
{
  "FeatureManagement": {
    "NewCheckoutFlow": {
      "EnabledFor": [
        {
          "Name": "Percentage",
          "Parameters": {
            "Value": 25
          }
        }
      ]
    }
  }
}
```

```csharp
// Registration
builder.Services.AddFeatureManagement()
    .AddFeatureFilter<PercentageFilter>();
```

### Time Window Filter

```json
{
  "FeatureManagement": {
    "HolidayPromotion": {
      "EnabledFor": [
        {
          "Name": "TimeWindow",
          "Parameters": {
            "Start": "2024-12-20T00:00:00Z",
            "End": "2024-12-26T23:59:59Z"
          }
        }
      ]
    }
  }
}
```

```csharp
builder.Services.AddFeatureManagement()
    .AddFeatureFilter<TimeWindowFilter>();
```

### Targeting Filter (User/Group Based)

```json
{
  "FeatureManagement": {
    "BetaFeatures": {
      "EnabledFor": [
        {
          "Name": "Targeting",
          "Parameters": {
            "Audience": {
              "Users": ["user1@example.com", "user2@example.com"],
              "Groups": [
                {
                  "Name": "BetaTesters",
                  "RolloutPercentage": 100
                },
                {
                  "Name": "InternalUsers",
                  "RolloutPercentage": 50
                }
              ],
              "DefaultRolloutPercentage": 0
            }
          }
        }
      ]
    }
  }
}
```

```csharp
// Registration with targeting context
builder.Services.AddFeatureManagement()
    .AddFeatureFilter<TargetingFilter>();

// Create targeting context
public class HttpContextTargetingContextAccessor : ITargetingContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTargetingContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask<TargetingContext> GetContextAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        var targetingContext = new TargetingContext
        {
            UserId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
            Groups = user?.FindAll("group").Select(c => c.Value).ToList() ?? new List<string>()
        };

        // Add tenant as a group
        var tenantId = user?.FindFirst("tenant")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            targetingContext.Groups.Add($"tenant:{tenantId}");
        }

        return ValueTask.FromResult(targetingContext);
    }
}

// Registration
builder.Services.AddSingleton<ITargetingContextAccessor, HttpContextTargetingContextAccessor>();
```

---

## 5. Custom Feature Filters

### Tenant-Based Filter

```csharp
// Filters/TenantFeatureFilter.cs
[FilterAlias("Tenant")]
public class TenantFeatureFilter : IFeatureFilter
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantFeatureFilter(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        var settings = context.Parameters.Get<TenantFilterSettings>();
        if (settings?.EnabledTenants == null || settings.EnabledTenants.Length == 0)
        {
            return Task.FromResult(false);
        }

        var currentTenant = _httpContextAccessor.HttpContext?.User
            .FindFirst("tenant")?.Value;

        if (string.IsNullOrEmpty(currentTenant))
        {
            return Task.FromResult(false);
        }

        var isEnabled = settings.EnabledTenants.Contains(currentTenant);
        return Task.FromResult(isEnabled);
    }
}

public class TenantFilterSettings
{
    public string[] EnabledTenants { get; set; } = Array.Empty<string>();
}

// Configuration
{
  "FeatureManagement": {
    "TenantSpecificFeature": {
      "EnabledFor": [
        {
          "Name": "Tenant",
          "Parameters": {
            "EnabledTenants": ["tenant-a", "tenant-b", "enterprise-corp"]
          }
        }
      ]
    }
  }
}

// Registration
builder.Services.AddFeatureManagement()
    .AddFeatureFilter<TenantFeatureFilter>();
```

### Environment Filter

```csharp
// Filters/EnvironmentFeatureFilter.cs
[FilterAlias("Environment")]
public class EnvironmentFeatureFilter : IFeatureFilter
{
    private readonly IHostEnvironment _environment;

    public EnvironmentFeatureFilter(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        var settings = context.Parameters.Get<EnvironmentFilterSettings>();

        if (settings?.Environments == null || settings.Environments.Length == 0)
        {
            return Task.FromResult(false);
        }

        var isEnabled = settings.Environments.Contains(
            _environment.EnvironmentName,
            StringComparer.OrdinalIgnoreCase);

        return Task.FromResult(isEnabled);
    }
}

public class EnvironmentFilterSettings
{
    public string[] Environments { get; set; } = Array.Empty<string>();
}

// Configuration - only enable in Development and Staging
{
  "FeatureManagement": {
    "DebugEndpoints": {
      "EnabledFor": [
        {
          "Name": "Environment",
          "Parameters": {
            "Environments": ["Development", "Staging"]
          }
        }
      ]
    }
  }
}
```

### Subscription Tier Filter

```csharp
// Filters/SubscriptionTierFilter.cs
[FilterAlias("SubscriptionTier")]
public class SubscriptionTierFilter : IFeatureFilter
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SubscriptionTierFilter(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        var settings = context.Parameters.Get<SubscriptionTierSettings>();

        var userTier = _httpContextAccessor.HttpContext?.User
            .FindFirst("subscription_tier")?.Value ?? "free";

        var tierOrder = new Dictionary<string, int>
        {
            ["free"] = 0,
            ["basic"] = 1,
            ["professional"] = 2,
            ["enterprise"] = 3
        };

        var requiredTierLevel = tierOrder.GetValueOrDefault(settings?.MinimumTier ?? "free", 0);
        var userTierLevel = tierOrder.GetValueOrDefault(userTier, 0);

        return Task.FromResult(userTierLevel >= requiredTierLevel);
    }
}

public class SubscriptionTierSettings
{
    public string MinimumTier { get; set; } = "free";
}

// Configuration
{
  "FeatureManagement": {
    "AdvancedAnalytics": {
      "EnabledFor": [
        {
          "Name": "SubscriptionTier",
          "Parameters": {
            "MinimumTier": "professional"
          }
        }
      ]
    },
    "BasicReports": {
      "EnabledFor": [
        {
          "Name": "SubscriptionTier",
          "Parameters": {
            "MinimumTier": "basic"
          }
        }
      ]
    }
  }
}
```

---

## 6. Feature Flag Service Integration

### Feature-Aware Service

```csharp
// Services/RecommendationService.cs
public interface IRecommendationService
{
    Task<IEnumerable<Recommendation>> GetRecommendationsAsync(int userId, CancellationToken ct);
}

public class RecommendationService : IRecommendationService
{
    private readonly IFeatureManager _featureManager;
    private readonly ISimpleRecommendationEngine _simpleEngine;
    private readonly IAiRecommendationEngine _aiEngine;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IFeatureManager featureManager,
        ISimpleRecommendationEngine simpleEngine,
        IAiRecommendationEngine aiEngine,
        ILogger<RecommendationService> logger)
    {
        _featureManager = featureManager;
        _simpleEngine = simpleEngine;
        _aiEngine = aiEngine;
        _logger = logger;
    }

    public async Task<IEnumerable<Recommendation>> GetRecommendationsAsync(
        int userId,
        CancellationToken ct)
    {
        if (await _featureManager.IsEnabledAsync("AiRecommendations"))
        {
            _logger.LogInformation("Using AI recommendation engine for user {UserId}", userId);
            return await _aiEngine.GetRecommendationsAsync(userId, ct);
        }

        _logger.LogInformation("Using simple recommendation engine for user {UserId}", userId);
        return await _simpleEngine.GetRecommendationsAsync(userId, ct);
    }
}
```

### Feature Variant Pattern

```csharp
// Get different behavior based on feature
public class SearchService : ISearchService
{
    private readonly IFeatureManagerSnapshot _featureManager;

    public async Task<SearchResult> SearchAsync(string query, CancellationToken ct)
    {
        var variant = await DetermineSearchVariantAsync();

        return variant switch
        {
            "v3-ai" => await SearchWithAiAsync(query, ct),
            "v2-optimized" => await SearchOptimizedAsync(query, ct),
            _ => await SearchBasicAsync(query, ct)
        };
    }

    private async Task<string> DetermineSearchVariantAsync()
    {
        if (await _featureManager.IsEnabledAsync("SearchV3"))
            return "v3-ai";

        if (await _featureManager.IsEnabledAsync("SearchV2"))
            return "v2-optimized";

        return "v1-basic";
    }
}
```

---

## 7. Feature Flag Endpoint

### Expose Feature Flags to Clients

```csharp
// Endpoint to check feature flags for frontend
app.MapGet("/api/features", async (
    IFeatureManager featureManager,
    HttpContext context) =>
{
    var features = new Dictionary<string, bool>();

    // List of features to expose to clients
    var clientFeatures = new[]
    {
        "DarkMode",
        "NewNavigation",
        "BetaFeatures",
        "AdvancedSearch"
    };

    foreach (var feature in clientFeatures)
    {
        features[feature] = await featureManager.IsEnabledAsync(feature);
    }

    return TypedResults.Ok(features);
})
.RequireAuthorization()
.WithName("GetFeatureFlags");

// Response:
// {
//   "DarkMode": true,
//   "NewNavigation": false,
//   "BetaFeatures": true,
//   "AdvancedSearch": false
// }
```

### Admin Endpoint for Feature Management

```csharp
// Admin endpoint to toggle features (for development/testing)
app.MapPost("/api/admin/features/{featureName}/toggle", async (
    string featureName,
    bool enabled,
    IConfiguration configuration) =>
{
    // Note: This is a simplified example. In production, use a proper
    // feature flag management system like Azure App Configuration,
    // LaunchDarkly, or similar.

    // This approach modifies in-memory configuration
    configuration[$"FeatureManagement:{featureName}"] = enabled.ToString();

    return TypedResults.Ok(new { feature = featureName, enabled });
})
.RequireAuthorization("admin")
.WithName("ToggleFeatureFlag");
```

---

## 8. Azure App Configuration Integration

### Setup for Cloud-Based Feature Flags

```xml
<PackageReference Include="Microsoft.Azure.AppConfiguration.AspNetCore" Version="7.0.0" />
<PackageReference Include="Microsoft.FeatureManagement.AspNetCore" Version="3.2.0" />
```

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Azure App Configuration
builder.Configuration.AddAzureAppConfiguration(options =>
{
    options
        .Connect(builder.Configuration["AzureAppConfiguration:ConnectionString"])
        .UseFeatureFlags(featureOptions =>
        {
            featureOptions.CacheExpirationInterval = TimeSpan.FromMinutes(5);
            featureOptions.Label = builder.Environment.EnvironmentName;
        });
});

builder.Services.AddAzureAppConfiguration();
builder.Services.AddFeatureManagement();

var app = builder.Build();

// Refresh configuration
app.UseAzureAppConfiguration();
```

---

## 9. Testing with Feature Flags

```csharp
public class FeatureFlagTests
{
    [Fact]
    public async Task Search_UsesNewAlgorithm_WhenFeatureEnabled()
    {
        // Arrange
        var featureManager = new Mock<IFeatureManager>();
        featureManager
            .Setup(f => f.IsEnabledAsync("NewSearchAlgorithm"))
            .ReturnsAsync(true);

        var service = new SearchService(
            featureManager.Object,
            Mock.Of<ISearchV1>(),
            Mock.Of<ISearchV2>());

        // Act
        var result = await service.SearchAsync("test", CancellationToken.None);

        // Assert - verify V2 algorithm was used
    }

    [Fact]
    public async Task Endpoint_Returns404_WhenFeatureDisabled()
    {
        // Arrange
        await using var app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["FeatureManagement:BetaFeatures"] = "false"
                    });
                });
            });

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/beta/feature");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Endpoint_RespectsFeatureFlag(bool featureEnabled)
    {
        // Arrange
        await using var app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["FeatureManagement:ExperimentalApi"] = featureEnabled.ToString()
                    });
                });
            });

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/experimental/test");

        // Assert
        if (featureEnabled)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
```

---

## 10. Complete Configuration Example

```json
{
  "FeatureManagement": {
    "SimpleFeature": true,

    "GradualRollout": {
      "EnabledFor": [
        {
          "Name": "Percentage",
          "Parameters": {
            "Value": 50
          }
        }
      ]
    },

    "BetaFeature": {
      "EnabledFor": [
        {
          "Name": "Targeting",
          "Parameters": {
            "Audience": {
              "Users": ["admin@company.com"],
              "Groups": [
                { "Name": "BetaTesters", "RolloutPercentage": 100 },
                { "Name": "Employees", "RolloutPercentage": 25 }
              ],
              "DefaultRolloutPercentage": 0
            }
          }
        }
      ]
    },

    "SeasonalPromotion": {
      "EnabledFor": [
        {
          "Name": "TimeWindow",
          "Parameters": {
            "Start": "2024-11-25T00:00:00Z",
            "End": "2024-11-30T23:59:59Z"
          }
        }
      ]
    },

    "EnterpriseFeature": {
      "EnabledFor": [
        {
          "Name": "Tenant",
          "Parameters": {
            "EnabledTenants": ["enterprise-a", "enterprise-b"]
          }
        }
      ]
    },

    "PremiumFeature": {
      "EnabledFor": [
        {
          "Name": "SubscriptionTier",
          "Parameters": {
            "MinimumTier": "professional"
          }
        }
      ]
    },

    "DevOnlyFeature": {
      "EnabledFor": [
        {
          "Name": "Environment",
          "Parameters": {
            "Environments": ["Development"]
          }
        }
      ]
    }
  }
}
```

---

## Summary

### Feature Filter Types

| Filter     | Use Case                          |
|------------|-----------------------------------|
| Boolean    | Simple on/off                     |
| Percentage | Gradual rollout                   |
| Targeting  | User/group based                  |
| TimeWindow | Time-limited features             |
| Custom     | Tenant, subscription, environment |

### Best Practices

1. **Name features clearly** — Use descriptive, consistent names
2. **Clean up old flags** — Remove flags after full rollout
3. **Document flags** — Track what each flag controls
4. **Test both states** — Ensure code works with flag on/off
5. **Use targeting for safety** — Start with internal users
6. **Monitor flag usage** — Track which flags are evaluated

### Flag Lifecycle

```
1. Create flag (disabled)
2. Deploy code behind flag
3. Enable for internal testing
4. Gradual rollout to users
5. Monitor and adjust
6. Full rollout (100%)
7. Remove flag from code
8. Delete flag configuration
```
