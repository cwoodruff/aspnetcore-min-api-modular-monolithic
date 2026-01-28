# Session 5: Repository Pattern & Data Access

**Duration:** 60 minutes
**Session Time:** 12:45 PM - 1:45 PM

---

## Overview

This session covers the Repository Pattern implementation using Entity Framework Core with SQLite. You'll understand the base repository with generic CRUD operations, entity-specific repositories with complex queries, and EF Core optimization techniques.

---

## Learning Objectives

By the end of this session, you will:
- Understand the Repository Pattern and its benefits
- Implement a generic base repository
- Create entity-specific repositories with custom queries
- Use EF Core's Include, ThenInclude, and AsSplitQuery
- Understand the difference between entities and API models

---

## Part 1: Repository Pattern Concepts (10 minutes)

### 1.1 Why Repository Pattern?

| Benefit | Description |
|---------|-------------|
| **Abstraction** | Hide EF Core details from services |
| **Testability** | Easy to mock for unit tests |
| **Single Responsibility** | Data access logic in one place |
| **Consistency** | Common patterns for all entities |

### 1.2 Solution Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Endpoints                               │
├─────────────────────────────────────────────────────────────┤
│                      Services (with caching)                 │
├─────────────────────────────────────────────────────────────┤
│                      Repositories                            │
├─────────────────────────────────────────────────────────────┤
│                      EF Core / AppDbContext                  │
├─────────────────────────────────────────────────────────────┤
│                      SQLite Database                         │
└─────────────────────────────────────────────────────────────┘
```

---

## Part 2: Repository Interface (10 minutes)

### 2.1 Base Repository Interface

**File: `src/Shared/SharedKernel.Persistence/Repositories/IRepository.cs`**

```csharp
namespace SharedKernel.Persistence.Repositories;

public interface IRepository<T>
{
    Task<bool> EntityExists(int id);
    Task<List<T>> GetAll();
    Task<T> Add(T entity);
    Task<bool> Update(T entity);
    Task<bool> Delete(int id);
}
```

### 2.2 Entity-Specific Interface Example

**File: `src/Shared/SharedKernel.Persistence/Repositories/IAlbumRepository.cs`**

```csharp
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.Repositories;

public interface IAlbumRepository : IRepository<Album>, IDisposable
{
    Task<List<Album>> GetByArtistId(int id);
    Task<AlbumApiModel> GetById(int id);
}
```

Key points:
- Extends `IRepository<Album>` for generic CRUD
- Adds custom methods for specific queries
- `GetById` returns `AlbumApiModel` (DTO) not entity
- Implements `IDisposable` for proper cleanup

---

## Part 3: Base Repository Implementation (15 minutes)

### 3.1 Complete Base Repository

**File: `src/Shared/SharedKernel.DataSQLite/Repositories/BaseRepository.cs`**

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
#pragma warning disable CA1051
    protected readonly AppDbContext _context;
#pragma warning restore CA1051

    protected BaseRepository(AppDbContext context)
    {
        _context = context;
    }

    public void Dispose() => _context.Dispose();

    public async Task<bool> EntityExists(int id) =>
        await _context.Set<T>().AsNoTracking().AnyAsync(a => a.Id == id);

    public async Task<List<T>> GetAll() =>
        await _context.Set<T>().AsNoTracking().ToListAsync();

    public async Task<T> Add(T entity)
    {
        await _context.Set<T>().AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> Update(T entity)
    {
        if (!await EntityExists(entity.Id))
            return false;
        _context.Set<T>().Update(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> Delete(int id)
    {
        if (!await EntityExists(id))
            return false;
        var toRemove = await _context.Set<T>().FindAsync(id);
        if (toRemove != null) _context.Set<T>().Remove(toRemove);
        await _context.SaveChangesAsync();
        return true;
    }

    public IQueryable<T> GetByCondition(Expression<Func<T, bool>> expression)
    {
        return _context.Set<T>()
            .Where(expression)
            .AsNoTracking();
    }
}
```

### 3.2 Key Implementation Details

#### AsNoTracking for Read Operations

```csharp
await _context.Set<T>().AsNoTracking().ToListAsync();
```

- Disables change tracking for read-only queries
- Improves performance
- Entities returned are not tracked by context

#### Generic Set<T> Access

```csharp
_context.Set<T>().AddAsync(entity);
```

- Works with any entity type
- No need for separate DbSet properties per method

#### Boolean Return for Mutations

```csharp
public async Task<bool> Update(T entity)
{
    if (!await EntityExists(entity.Id))
        return false;
    // ... update logic
    return true;
}
```

- Returns success/failure indicator
- Allows callers to handle not-found cases

---

## Part 4: Entity-Specific Repositories (20 minutes)

### 4.1 Album Repository with Complex Queries

**File: `src/Shared/SharedKernel.DataSQLite/Repositories/AlbumRepository.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;

namespace SharedKernel.DataSQLite.Repositories;

public class AlbumRepository(AppDbContext context) : BaseRepository<Album>(context), IAlbumRepository
{
    public async Task<List<Album>> GetByArtistId(int id) =>
        await _context.Albums
            .Where(a => a.ArtistId == id)
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .AsNoTracking()
            .ToListAsync();

    public async Task<AlbumApiModel> GetById(int id)
    {
        // Album with Tracks (and Artist) via split queries, no tracking
        var albumEntity = await _context.Albums
            .Where(a => a.Id == id)
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .ThenInclude(t => t.Genre)       // optional if you need GenreName
            .Include(a => a.Tracks)
            .ThenInclude(t => t.MediaType)   // optional if you need MediaTypeName
            .AsNoTracking()
            .AsSplitQuery()   // important on SQLite to avoid cartesian explosion
            .SingleAsync();

        var albumDto = new AlbumApiModel
        {
            Id = albumEntity.Id,
            Title = albumEntity.Title,
            ArtistId = albumEntity.ArtistId,
            ArtistName = albumEntity.Artist?.Name,
            Artist = albumEntity.Artist == null ? null : new ArtistApiModel
            {
                Id = albumEntity.Artist.Id,
                Name = albumEntity.Artist.Name
            },
            Tracks = albumEntity.Tracks.Select(t => new TrackApiModel
            {
                Id = t.Id,
                Name = t.Name,
                AlbumId = t.AlbumId,
                GenreId = t.GenreId,
                MediaTypeId = t.MediaTypeId,
                Composer = t.Composer,
                Milliseconds = t.Milliseconds,
                Bytes = t.Bytes,
                UnitPrice = t.UnitPrice,
                AlbumName = albumEntity.Title,
                GenreName = t.Genre?.Name,
                MediaTypeName = t.MediaType?.Name,
                // keep nested objects null to avoid cycles
                Album = null,
                Genre = null,
                MediaType = null,
                Playlists = new List<PlaylistApiModel>(),
                InvoiceLines = new List<InvoiceLineApiModel>()
            }).ToList()
        };
        return albumDto;
    }
}
```

### 4.2 Key EF Core Patterns

#### Include and ThenInclude

```csharp
.Include(a => a.Artist)           // Load related Artist
.Include(a => a.Tracks)           // Load related Tracks
.ThenInclude(t => t.Genre)        // For each Track, load Genre
```

- `Include` - loads directly related entities
- `ThenInclude` - loads nested related entities

#### AsSplitQuery for SQLite

```csharp
.AsSplitQuery()
```

- Splits complex includes into multiple SQL queries
- Avoids cartesian product explosion
- Essential for SQLite with multiple collections

#### Entity to DTO Projection

```csharp
var albumDto = new AlbumApiModel
{
    Id = albumEntity.Id,
    Title = albumEntity.Title,
    // ... map all properties
};
```

- Projects entity to API model in repository
- Avoids circular references in JSON
- Controls exactly what data is returned

---

## Part 5: Entity and API Model Definitions

### 5.1 Base Entity

**File: `src/Shared/SharedKernel.Persistence/Entities/BaseEntity.cs`**

```csharp
namespace SharedKernel.Persistence.Entities;

public class BaseEntity
{
    public int Id { get; set; }
}
```

### 5.2 Album Entity

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

### 5.3 AppDbContext Configuration

**File: `src/Shared/SharedKernel.Persistence/AppDbContext.cs` (partial)**

```csharp
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Album> Albums { get; set; }
    public DbSet<Artist> Artists { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceLine> InvoiceLines { get; set; }
    public DbSet<MediaType> MediaTypes { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<PlaylistTrack> PlaylistTracks { get; set; }
    public DbSet<Track> Tracks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("Album");
            entity.Property(e => e.Title).HasColumnType("nvarchar(160)");
            entity.HasOne(d => d.Artist).WithMany(p => p.Albums).HasForeignKey(d => d.ArtistId);
        });

        modelBuilder.Entity<Artist>(entity =>
        {
            entity.ToTable("Artist");
            entity.Property(e => e.Name).HasColumnType("nvarchar(120)");
        });

        // ... more entity configurations
    }
}
```

---

## Part 6: Repository Registration (5 minutes)

### 6.1 Registration in Program.cs

```csharp
// Data Repositories
builder.Services.AddScoped<IAlbumRepository, AlbumRepository>()
    .AddScoped<IArtistRepository, ArtistRepository>()
    .AddScoped<ICustomerRepository, CustomerRepository>()
    .AddScoped<IEmployeeRepository, EmployeeRepository>()
    .AddScoped<IGenreRepository, GenreRepository>()
    .AddScoped<IInvoiceRepository, InvoiceRepository>()
    .AddScoped<IInvoiceLineRepository, InvoiceLineRepository>()
    .AddScoped<IMediaTypeRepository, MediaTypeRepository>()
    .AddScoped<IPlaylistRepository, PlaylistRepository>()
    .AddScoped<ITrackRepository, TrackRepository>();
```

### 6.2 Scoped Lifetime

- Repositories are registered as `Scoped`
- One instance per HTTP request
- Matches DbContext lifetime
- Ensures proper change tracking

---

## Checkpoint

Before moving to Session 6, verify:

- [ ] Understand Repository Pattern benefits
- [ ] Know how base repository provides generic CRUD
- [ ] Can create entity-specific repositories
- [ ] Understand Include/ThenInclude for eager loading
- [ ] Know when to use AsSplitQuery
- [ ] Understand entity to DTO projection

---

## Quick Reference

### Repository Pattern Template

```csharp
// Interface
public interface IYourEntityRepository : IRepository<YourEntity>, IDisposable
{
    Task<YourApiModel> GetById(int id);
    Task<List<YourEntity>> GetByCustomCriteria(string criteria);
}

// Implementation
public class YourEntityRepository(AppDbContext context)
    : BaseRepository<YourEntity>(context), IYourEntityRepository
{
    public async Task<YourApiModel> GetById(int id)
    {
        var entity = await _context.YourEntities
            .Where(e => e.Id == id)
            .Include(e => e.RelatedEntity)
            .AsNoTracking()
            .SingleAsync();

        return new YourApiModel { /* map properties */ };
    }
}
```

### EF Core Query Patterns

| Pattern | Purpose |
|---------|---------|
| `.AsNoTracking()` | Disable change tracking (reads) |
| `.Include(e => e.Nav)` | Eager load navigation property |
| `.ThenInclude(n => n.Child)` | Eager load nested navigation |
| `.AsSplitQuery()` | Split into multiple SQL queries |
| `.Where(e => predicate)` | Filter results |
| `.SingleAsync()` | Expect exactly one result |
| `.ToListAsync()` | Get all matching results |

---

## Next Session

In **Session 6: Service Layer with Caching**, you will:
- Build the service layer that wraps repositories
- Implement cache-aside pattern with ICacheFacade
- Use structured cache keys
- Apply tag-based cache invalidation on writes
