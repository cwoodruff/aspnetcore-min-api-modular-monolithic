# Session 1: Welcome & Environment Setup

**Duration:** 30 minutes
**Session Time:** 8:00 AM - 8:30 AM

---

## Overview

This session prepares your development environment and introduces you to the
workshop solution structure. You'll verify installations, clone the starter
solution, and understand the foundational components you'll be building upon.

---

## Learning Objectives

By the end of this session, you will:

- Have a working .NET 10 development environment
- Successfully build and run the starter solution
- Understand the project structure and module organization
- Be familiar with the Chinook database schema

---

## Prerequisites

### Required Software

| Software          | Minimum Version | Verify Command      |
|-------------------|-----------------|---------------------|
| .NET SDK          | 10.0            | `dotnet --version`  |
| Git               | 2.30+           | `git --version`     |
| SQLite (optional) | 3.x             | `sqlite3 --version` |

### Recommended IDEs

Choose one of the following:

- **JetBrains Rider** (recommended for this workshop)
- **Visual Studio 2022** (17.0+)
- **VS Code** with C# Dev Kit extension

---

## Step 1: Environment Verification (10 minutes)

### 1.1 Check .NET SDK

Open your terminal and run:

```bash
dotnet --version
```

Expected output: `10.0.x` (or 9.0+ minimum)

If not installed, download from: https://dotnet.microsoft.com/download

### 1.2 Check Git

```bash
git --version
```

Expected output: `git version 2.x.x`

### 1.3 Verify SQLite (Optional)

```bash
sqlite3 --version
```

SQLite comes bundled with the solution's EF Core provider, so this is optional
for database browsing.

---

## Step 2: Clone and Build (5 minutes)

### 2.1 Clone the Repository

```bash
git clone <workshop-repo-url> workshop-starter
cd workshop-starter
```

### 2.2 Restore Dependencies

```bash
dotnet restore
```

### 2.3 Build the Solution

```bash
dotnet build
```

Expected: Build succeeded with no errors.

### 2.4 Run Initial Test

```bash
dotnet run --project src/ModularMonolith.Api
```

Navigate to: http://localhost:5043/swagger

You should see the Swagger UI with available endpoints.

---

## Step 3: Explore the Solution Structure (15 minutes)

### 3.1 Solution Overview

The solution follows a Modular Monolithic architecture:

```
/src
  /ModularMonolith.Api          # Host application (entry point)
    - Program.cs                # Main composition and startup
    - appsettings.json          # Configuration
    - data/chinook.db           # SQLite database

  /Modules                      # Feature modules
    /Music/Music.Module         # Music catalog (Albums, Artists, Tracks, Playlists)
    /Orders/Orders.Module       # Order processing (Invoices, InvoiceLines)
    /Administration/Admin.Module # Admin functions (Customers, Employees, Genres)
    /Reporting/Reporting.Module # Reports and health monitoring
    /Identity/Identity.Module   # Authentication and authorization

  /Shared                       # Shared infrastructure
    /SharedKernel               # Core abstractions (IModule, caching, etc.)
    /SharedKernel.Persistence   # EF Core, entities, API models, validation
    /SharedKernel.DataSQLite    # SQLite repository implementations

/tests
  /ModularMonolith.Api.Tests    # Integration tests
  /ModularMonolith.Services.Tests # Service unit tests
```

### 3.2 Key File: IModule Contract

The foundation of our modular architecture is the `IModule` interface:

**File: `src/Shared/SharedKernel/IModule.cs`**

```csharp
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel;

public interface IModule
{
    string Name { get; }
    void RegisterServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

Every module in the system implements this interface, enabling:

- **Self-contained service registration** - Each module registers its own
  dependencies
- **Endpoint mapping** - Each module defines its own API routes
- **Clean composition** - The host simply iterates through modules

### 3.3 Key File: Program.cs (Host Composition)

The host application composes all modules:

**File: `src/ModularMonolith.Api/Program.cs` (Key Sections)**

```csharp
// Register module services BEFORE building the app
var modules = GetModules();
foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

// Map module endpoints
foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

static IReadOnlyList<IModule> GetModules()
{
    return
    [
        new AdministrationModule.Modules(),
        new IdentityModule.Modules(),
        new MusicModule.Modules(),
        new OrdersModule.Modules(),
        new ReportingModule.Modules()
    ];
}
```

### 3.4 Database: Chinook Schema

The workshop uses the Chinook database - a sample music store database:

| Entity      | Description      | Module         |
|-------------|------------------|----------------|
| Album       | Music albums     | Music          |
| Artist      | Artists/bands    | Music          |
| Track       | Individual songs | Music          |
| Playlist    | User playlists   | Music          |
| Customer    | Store customers  | Administration |
| Employee    | Store employees  | Administration |
| Genre       | Music genres     | Administration |
| MediaType   | File formats     | Administration |
| Invoice     | Customer orders  | Orders         |
| InvoiceLine | Order line items | Orders         |

### 3.5 Entity Example

**File: `src/Shared/SharedKernel.Persistence/Entities/Album.cs`**

```csharp
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Converters;

namespace SharedKernel.Persistence.Entities;

public partial class Album : BaseEntity, IConvertModel<AlbumApiModel>
{
    public string? Title { get; set; }

    public int? ArtistId { get; set; }

    public virtual Artist? Artist { get; set; }

    public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();

    public AlbumApiModel Convert() =>
        new()
        {
            Id = Id,
            ArtistId = ArtistId,
            Title = Title,
            ArtistName = Artist?.Name
        };
}
```

**File: `src/Shared/SharedKernel.Persistence/Entities/BaseEntity.cs`**

```csharp
namespace SharedKernel.Persistence.Entities;

public class BaseEntity
{
    public int Id { get; set; }
}
```

---

## Step 4: Verify Database Connection

### 4.1 Check Database File

The SQLite database is located at: `data/chinook.db`

### 4.2 Database Context Configuration

**File: `src/Shared/SharedKernel.Persistence/PersistenceRegistration.cs`**

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Persistence.Validation;

namespace SharedKernel.Persistence;

public static class PersistenceRegistration
{
    private const string ConnectionName = "AppDatabase";

    public static IServiceCollection AddKernelPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fallback: build absolute path relative to current content root
            var baseDir = AppContext.BaseDirectory;
            var current = new DirectoryInfo(baseDir);
            while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "data")))
            {
                current = current.Parent;
            }
            var root = current?.FullName ?? AppContext.BaseDirectory;
            var dbPath = Path.Combine(root, "data", "chinook.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            connectionString = $"Data Source={dbPath}";
        }

        services.AddDbContextPool<AppDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString, sqlite =>
            {
                sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });
        }, poolSize: 128);

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<CustomerValidator>();

        return services;
    }
}
```

---

## Checkpoint

Before moving to Session 2, verify:

- [ ] .NET SDK 10.0+ installed and verified
- [ ] Repository cloned successfully
- [ ] Solution builds without errors
- [ ] Application runs and Swagger UI accessible
- [ ] Understand the IModule contract
- [ ] Familiar with solution structure

---

## Troubleshooting

### Build Errors

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Port Already in Use

Modify `launchSettings.json` or use:

```bash
dotnet run --project src/ModularMonolith.Api --urls "http://localhost:5044"
```

### Database Not Found

The auto-discovery logic in `PersistenceRegistration.cs` walks up the directory
tree looking for the `data/` folder. If you've moved the project, ensure
`data/chinook.db` exists relative to the solution root.

---

## Quick Reference

| Command                                        | Purpose                |
|------------------------------------------------|------------------------|
| `dotnet build`                                 | Build the solution     |
| `dotnet run --project src/ModularMonolith.Api` | Run the API            |
| `dotnet test`                                  | Run all tests          |
| `dotnet restore`                               | Restore NuGet packages |

---

## Next Session

In **Session 2: Architecture Overview & Module Contract**, you will:

- Deep dive into Modular Monolithic architecture
- Understand the benefits vs. microservices
- Configure the host for module composition
- Create your first module implementation
