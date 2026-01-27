# Workshop Starter Solution Guide

This document describes the starter solution that workshop participants receive. It includes the foundational infrastructure so participants can focus on learning architectural patterns rather than boilerplate setup.

---

## Starter Solution Contents

### What's Included (Pre-Built)

#### 1. Solution Structure
```
workshop-starter/
├── src/
│   ├── ModularMonolith.Api/
│   │   ├── Program.cs              # Minimal setup (see below)
│   │   ├── appsettings.json        # Database connection configured
│   │   ├── appsettings.Development.json
│   │   └── data/
│   │       └── chinook.db          # SQLite database with sample data
│   ├── Modules/
│   │   ├── Music/Music.Module/
│   │   │   └── Music.Module.csproj
│   │   ├── Orders/Orders.Module/
│   │   │   └── Orders.Module.csproj
│   │   ├── Administration/Admin.Module/
│   │   │   └── Admin.Module.csproj
│   │   ├── Reporting/Reporting.Module/
│   │   │   └── Reporting.Module.csproj
│   │   └── Identity/Identity.Module/
│   │       └── Identity.Module.csproj
│   └── Shared/
│       ├── SharedKernel/
│       │   ├── SharedKernel.csproj
│       │   ├── IModule.cs          # Module contract interface
│       │   └── Caching/            # Full caching infrastructure
│       │       ├── ICacheFacade.cs
│       │       ├── CompositeCacheFacade.cs
│       │       ├── ICacheKeyComposer.cs
│       │       ├── CacheKeyComposer.cs
│       │       ├── CacheEntryOptions.cs
│       │       └── CachingRegistration.cs
│       ├── SharedKernel.Persistence/
│       │   ├── SharedKernel.Persistence.csproj
│       │   ├── AppDbContext.cs     # Fully configured
│       │   ├── IAppDbContext.cs
│       │   ├── PersistenceRegistration.cs
│       │   ├── Entities/           # All Chinook entities
│       │   │   ├── Album.cs
│       │   │   ├── Artist.cs
│       │   │   ├── Customer.cs
│       │   │   ├── Employee.cs
│       │   │   ├── Genre.cs
│       │   │   ├── Invoice.cs
│       │   │   ├── InvoiceLine.cs
│       │   │   ├── MediaType.cs
│       │   │   ├── Playlist.cs
│       │   │   ├── PlaylistTrack.cs
│       │   │   └── Track.cs
│       │   ├── ApiModels/          # All DTOs
│       │   │   ├── AlbumApiModel.cs
│       │   │   ├── ArtistApiModel.cs
│       │   │   ├── CustomerApiModel.cs
│       │   │   └── ... (all models)
│       │   ├── Repositories/       # Interfaces only
│       │   │   ├── IRepository.cs
│       │   │   ├── IAlbumRepository.cs
│       │   │   ├── IArtistRepository.cs
│       │   │   └── ... (all interfaces)
│       │   └── Extensions/
│       │       └── ConvertExtensions.cs
│       └── SharedKernel.DataSQLite/
│           ├── SharedKernel.DataSQLite.csproj
│           └── Repositories/       # Empty - to be built in workshop
├── tests/
│   └── ModularMonolith.Api.Tests/
│       ├── ModularMonolith.Api.Tests.csproj
│       └── GlobalUsings.cs
├── ModularMonolith.Api.sln
└── Directory.Build.props
```

### What Participants Build

During the workshop, participants will create:

1. **Repository Implementations** (Session 5)
   - `BaseRepository<T>`
   - `GenreRepository`
   - `ArtistRepository`
   - `AlbumRepository`

2. **Identity Module** (Session 4)
   - `DevKeyMaterialService`
   - `TokenService`
   - `InMemoryUserStore`
   - `InMemoryRefreshTokenStore`
   - `AuthEndpoints`
   - `IdentityAuthExtensions`

3. **Module Implementations** (Sessions 2-3)
   - `ReportingModule.Modules`
   - `MusicModule.Modules`
   - `AdministrationModule.Modules`

4. **Services** (Session 6)
   - `IGenreService` / `GenreService`
   - `IArtistService` / `ArtistService`

5. **Validators** (Session 7)
   - `GenreValidator`
   - `ArtistValidator`

6. **Tests** (Session 9)
   - Health endpoint tests
   - Authenticated endpoint tests

---

## Starter Program.cs

The starter `Program.cs` should be minimal:

```csharp
using SharedKernel;
using SharedKernel.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Modular Monolith API", Version = "v1" });
});

builder.Services.AddProblemDetails();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000",
            "http://localhost:4200",
            "http://localhost:5173")
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});

// Configure database connection string
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "data", "chinook.db");
if (File.Exists(dbPath))
{
    builder.Configuration["ConnectionStrings:AppDatabase"] = $"Data Source={dbPath}";
}

// Add persistence
builder.Services.AddKernelPersistence(builder.Configuration);

// Add caching
builder.Services.AddCentralCaching(builder.Configuration);

// TODO: Register modules here during workshop
// var modules = GetModules();
// foreach (var module in modules)
// {
//     module.RegisterServices(builder.Services, builder.Configuration);
// }

var app = builder.Build();

// Configure pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Default");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Root endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "root",
    status = "Healthy",
    service = "ModularMonolith.Api",
    timestampUtc = DateTime.UtcNow
}));

// TODO: Map module endpoints during workshop
// foreach (var module in modules)
// {
//     module.MapEndpoints(app);
// }

app.Run();

// TODO: Uncomment and populate during workshop
// static IReadOnlyList<IModule> GetModules() => [];

public partial class Program { }
```

---

## Starter appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ServiceName": "mmapi",
  "Jwt": {
    "Issuer": "modular-monolith-api",
    "Audience": "modular-api",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7,
    "KeyProvider": "Dev"
  },
  "Caching": {
    "Tier": "L1",
    "Provider": "InMemory"
  }
}
```

---

## Creating the Starter Solution

### Option 1: Script (Recommended)

```bash
#!/bin/bash
# create-workshop-starter.sh

COMPLETE_SOLUTION="."
STARTER_DIR="workshop-starter"

# Create clean starter directory
rm -rf "$STARTER_DIR"
mkdir -p "$STARTER_DIR"

# Copy solution files
cp "$COMPLETE_SOLUTION/ModularMonolith.Api.sln" "$STARTER_DIR/"
cp "$COMPLETE_SOLUTION/Directory.Build.props" "$STARTER_DIR/"

# Copy src structure
mkdir -p "$STARTER_DIR/src"

# Copy API host with starter Program.cs
mkdir -p "$STARTER_DIR/src/ModularMonolith.Api/data"
cp "$COMPLETE_SOLUTION/src/ModularMonolith.Api/ModularMonolith.Api.csproj" "$STARTER_DIR/src/ModularMonolith.Api/"
cp "$COMPLETE_SOLUTION/src/ModularMonolith.Api/appsettings.json" "$STARTER_DIR/src/ModularMonolith.Api/"
cp "$COMPLETE_SOLUTION/src/ModularMonolith.Api/appsettings.Development.json" "$STARTER_DIR/src/ModularMonolith.Api/"
cp "$COMPLETE_SOLUTION/src/ModularMonolith.Api/data/chinook.db" "$STARTER_DIR/src/ModularMonolith.Api/data/"
# Create starter Program.cs manually (see above)

# Copy SharedKernel (full)
cp -r "$COMPLETE_SOLUTION/src/Shared/SharedKernel" "$STARTER_DIR/src/Shared/"

# Copy SharedKernel.Persistence (full - entities, models, interfaces)
cp -r "$COMPLETE_SOLUTION/src/Shared/SharedKernel.Persistence" "$STARTER_DIR/src/Shared/"
# Remove validator implementations if you want participants to build them
# rm -rf "$STARTER_DIR/src/Shared/SharedKernel.Persistence/Validation"

# Copy SharedKernel.DataSQLite (structure only - no implementations)
mkdir -p "$STARTER_DIR/src/Shared/SharedKernel.DataSQLite/Repositories"
cp "$COMPLETE_SOLUTION/src/Shared/SharedKernel.DataSQLite/SharedKernel.DataSQLite.csproj" "$STARTER_DIR/src/Shared/SharedKernel.DataSQLite/"

# Copy module projects (structure only)
for module in Music Orders Administration Reporting Identity; do
    module_path=$(find "$COMPLETE_SOLUTION/src/Modules" -name "*${module}*" -type d | head -1)
    if [ -d "$module_path" ]; then
        target_dir="$STARTER_DIR/src/Modules/$(dirname ${module_path#$COMPLETE_SOLUTION/src/Modules/})/${module}.Module"
        mkdir -p "$target_dir"
        cp "$module_path"/*.csproj "$target_dir/" 2>/dev/null || true
    fi
done

# Create empty directories for module implementations
mkdir -p "$STARTER_DIR/src/Modules/Music/Music.Module/Services"
mkdir -p "$STARTER_DIR/src/Modules/Music/Music.Module/Endpoints"
mkdir -p "$STARTER_DIR/src/Modules/Orders/Orders.Module/Services"
mkdir -p "$STARTER_DIR/src/Modules/Orders/Orders.Module/Endpoints"
mkdir -p "$STARTER_DIR/src/Modules/Administration/Admin.Module/Services"
mkdir -p "$STARTER_DIR/src/Modules/Administration/Admin.Module/Endpoints"
mkdir -p "$STARTER_DIR/src/Modules/Reporting/Reporting.Module/Endpoints"
mkdir -p "$STARTER_DIR/src/Modules/Identity/Identity.Module/Services"
mkdir -p "$STARTER_DIR/src/Modules/Identity/Identity.Module/Endpoints"
mkdir -p "$STARTER_DIR/src/Modules/Identity/Identity.Module/Authorization"
mkdir -p "$STARTER_DIR/src/Modules/Identity/Identity.Module/KeyManagement"
mkdir -p "$STARTER_DIR/src/Modules/Identity/Identity.Module/Extensions"

# Copy test project (structure only)
mkdir -p "$STARTER_DIR/tests/ModularMonolith.Api.Tests"
cp "$COMPLETE_SOLUTION/tests/ModularMonolith.Api.Tests/ModularMonolith.Api.Tests.csproj" "$STARTER_DIR/tests/ModularMonolith.Api.Tests/"
cp "$COMPLETE_SOLUTION/tests/ModularMonolith.Api.Tests/GlobalUsings.cs" "$STARTER_DIR/tests/ModularMonolith.Api.Tests/" 2>/dev/null || true

echo "Workshop starter solution created in $STARTER_DIR"
echo "Remember to:"
echo "1. Create the starter Program.cs"
echo "2. Verify the solution builds: dotnet build"
echo "3. Test database connectivity"
```

### Option 2: Git Branch

Create a `workshop-starter` branch with the minimal implementation:

```bash
git checkout -b workshop-starter
# Remove implementations, commit
git push origin workshop-starter
```

---

## Pre-Workshop Checklist

### For Instructors

- [ ] Starter solution builds without errors
- [ ] Database file (chinook.db) is included and accessible
- [ ] All NuGet packages restore correctly
- [ ] Swagger UI loads at http://localhost:5043/swagger
- [ ] Test project compiles (even if no tests exist yet)
- [ ] USB drives prepared with offline copies
- [ ] Presentation slides ready
- [ ] Room setup with projector/screen

### For Participants

- [ ] .NET SDK 10.0 installed
- [ ] IDE installed (Rider, VS 2022, or VS Code)
- [ ] Git installed
- [ ] Internet access (or offline package cache)
- [ ] GitHub/GitLab account (if using cloud repo)

---

## Verification Steps

After creating the starter solution, verify:

```bash
cd workshop-starter
dotnet restore
dotnet build

# Should succeed with warnings about empty projects
# Should NOT have any errors

dotnet run --project src/ModularMonolith.Api
# Should start on http://localhost:5043
# Root endpoint should return health JSON
# Swagger should load at /swagger
```

---

## Module Project Files

Ensure each module project has correct references:

### Music.Module.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\Shared\SharedKernel\SharedKernel.csproj" />
    <ProjectReference Include="..\..\..\..\Shared\SharedKernel.Persistence\SharedKernel.Persistence.csproj" />
    <ProjectReference Include="..\..\..\..\Shared\SharedKernel.DataSQLite\SharedKernel.DataSQLite.csproj" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
```

### Identity.Module.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\Shared\SharedKernel\SharedKernel.csproj" />
    <ProjectReference Include="..\..\..\..\Shared\SharedKernel.Persistence\SharedKernel.Persistence.csproj" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0-*" />
  </ItemGroup>
</Project>
```

---

## Troubleshooting Setup Issues

### "Project reference not found"
- Verify all .csproj files exist
- Check relative paths in project references
- Run `dotnet restore` from solution root

### "Database not found"
- Ensure chinook.db is in `src/ModularMonolith.Api/data/`
- Check connection string in appsettings.json
- Verify file permissions

### "Package restore failed"
- Check internet connectivity
- Clear NuGet cache: `dotnet nuget locals all --clear`
- Use offline package source if needed

### "SDK not found"
- Install .NET 10 SDK from https://dot.net
- Verify with `dotnet --list-sdks`
- Check global.json if present
