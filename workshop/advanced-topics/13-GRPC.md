# gRPC Services Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

gRPC is a high-performance RPC framework using Protocol Buffers for serialization. It's ideal for microservice-to-microservice communication, low-latency scenarios, and strongly-typed contracts.

**Duration:** 45-60 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of RPC concepts

---

## Learning Objectives

By the end of this guide, you will:
- Understand when to use gRPC vs REST
- Create .proto files and generate C# code
- Implement gRPC services
- Call gRPC services from clients
- Add authentication and error handling

---

## 1. gRPC vs REST

### Comparison

| Aspect | REST | gRPC |
|--------|------|------|
| Protocol | HTTP/1.1 or HTTP/2 | HTTP/2 only |
| Serialization | JSON (text) | Protocol Buffers (binary) |
| Contract | OpenAPI (optional) | .proto (required) |
| Streaming | Limited | Native support |
| Browser support | Full | Limited (needs proxy) |
| Performance | Good | Excellent |
| Debugging | Easy | Requires tooling |

### When to Use gRPC

- **Service-to-service** communication
- **High performance** requirements
- **Streaming** data (bidirectional)
- **Polyglot** environments
- **Strict contracts** needed

### When to Use REST

- **Browser clients**
- **Public APIs**
- **Simple CRUD** operations
- **Cacheable** responses

---

## 2. Project Setup

### Add NuGet Packages

```xml
<!-- Server -->
<PackageReference Include="Grpc.AspNetCore" Version="2.60.0" />

<!-- Client -->
<PackageReference Include="Grpc.Net.Client" Version="2.60.0" />
<PackageReference Include="Google.Protobuf" Version="3.25.0" />
<PackageReference Include="Grpc.Tools" Version="2.60.0" PrivateAssets="All" />
```

### Configure Project for Protobuf

```xml
<!-- In .csproj -->
<ItemGroup>
  <Protobuf Include="Protos\*.proto" GrpcServices="Server" />
</ItemGroup>
```

---

## 3. Define Service Contract (.proto)

### Album Service

```protobuf
// Protos/album.proto
syntax = "proto3";

option csharp_namespace = "ModularMonolith.Grpc";

package music;

import "google/protobuf/empty.proto";
import "google/protobuf/timestamp.proto";
import "google/protobuf/wrappers.proto";

// Album service definition
service AlbumService {
  // Unary RPCs
  rpc GetAlbum (GetAlbumRequest) returns (AlbumResponse);
  rpc CreateAlbum (CreateAlbumRequest) returns (AlbumResponse);
  rpc UpdateAlbum (UpdateAlbumRequest) returns (AlbumResponse);
  rpc DeleteAlbum (DeleteAlbumRequest) returns (google.protobuf.Empty);

  // Server streaming
  rpc ListAlbums (ListAlbumsRequest) returns (stream AlbumResponse);

  // Client streaming
  rpc BatchCreateAlbums (stream CreateAlbumRequest) returns (BatchCreateResponse);

  // Bidirectional streaming
  rpc SyncAlbums (stream AlbumSyncRequest) returns (stream AlbumSyncResponse);
}

// Request messages
message GetAlbumRequest {
  int32 id = 1;
}

message ListAlbumsRequest {
  int32 page = 1;
  int32 page_size = 2;
  google.protobuf.StringValue search = 3;
  google.protobuf.Int32Value artist_id = 4;
}

message CreateAlbumRequest {
  string title = 1;
  int32 artist_id = 2;
  google.protobuf.Int32Value genre_id = 3;
}

message UpdateAlbumRequest {
  int32 id = 1;
  string title = 2;
  int32 artist_id = 3;
}

message DeleteAlbumRequest {
  int32 id = 1;
}

message AlbumSyncRequest {
  string client_id = 1;
  int64 last_sync_timestamp = 2;
}

// Response messages
message AlbumResponse {
  int32 id = 1;
  string title = 2;
  ArtistInfo artist = 3;
  repeated TrackInfo tracks = 4;
  google.protobuf.Timestamp created_at = 5;
}

message ArtistInfo {
  int32 id = 1;
  string name = 2;
}

message TrackInfo {
  int32 id = 1;
  string name = 2;
  int32 duration_ms = 3;
}

message BatchCreateResponse {
  int32 created_count = 1;
  repeated int32 created_ids = 2;
}

message AlbumSyncResponse {
  string action = 1;  // "created", "updated", "deleted"
  AlbumResponse album = 2;
}
```

---

## 4. Implement gRPC Service

```csharp
// Services/AlbumGrpcService.cs
using Grpc.Core;
using ModularMonolith.Grpc;

public class AlbumGrpcService : AlbumService.AlbumServiceBase
{
    private readonly IAlbumRepository _repository;
    private readonly ILogger<AlbumGrpcService> _logger;

    public AlbumGrpcService(
        IAlbumRepository repository,
        ILogger<AlbumGrpcService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // Unary RPC
    public override async Task<AlbumResponse> GetAlbum(
        GetAlbumRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("GetAlbum called for ID: {Id}", request.Id);

        var album = await _repository.GetById(request.Id);

        if (album is null)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound,
                $"Album {request.Id} not found"));
        }

        return MapToResponse(album);
    }

    // Server streaming
    public override async Task ListAlbums(
        ListAlbumsRequest request,
        IServerStreamWriter<AlbumResponse> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("ListAlbums streaming started");

        var albums = await _repository.GetAll();

        // Apply filters
        if (request.Search is not null)
        {
            albums = albums.Where(a =>
                a.Title.Contains(request.Search.Value, StringComparison.OrdinalIgnoreCase));
        }

        if (request.ArtistId is not null)
        {
            albums = albums.Where(a => a.ArtistId == request.ArtistId.Value);
        }

        // Stream results
        foreach (var album in albums)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            await responseStream.WriteAsync(MapToResponse(album));
        }

        _logger.LogInformation("ListAlbums streaming completed");
    }

    // Client streaming
    public override async Task<BatchCreateResponse> BatchCreateAlbums(
        IAsyncStreamReader<CreateAlbumRequest> requestStream,
        ServerCallContext context)
    {
        var createdIds = new List<int>();

        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            var album = new Album
            {
                Title = request.Title,
                ArtistId = request.ArtistId
            };

            await _repository.Add(album);
            createdIds.Add(album.AlbumId);
        }

        return new BatchCreateResponse
        {
            CreatedCount = createdIds.Count,
            CreatedIds = { createdIds }
        };
    }

    // Bidirectional streaming
    public override async Task SyncAlbums(
        IAsyncStreamReader<AlbumSyncRequest> requestStream,
        IServerStreamWriter<AlbumSyncResponse> responseStream,
        ServerCallContext context)
    {
        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            // Get changes since last sync
            var changes = await _repository.GetChangesSince(
                DateTimeOffset.FromUnixTimeMilliseconds(request.LastSyncTimestamp));

            foreach (var change in changes)
            {
                await responseStream.WriteAsync(new AlbumSyncResponse
                {
                    Action = change.Action,
                    Album = MapToResponse(change.Album)
                });
            }
        }
    }

    public override async Task<AlbumResponse> CreateAlbum(
        CreateAlbumRequest request,
        ServerCallContext context)
    {
        // Validate
        if (string.IsNullOrEmpty(request.Title))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Title is required"));
        }

        var album = new Album
        {
            Title = request.Title,
            ArtistId = request.ArtistId
        };

        await _repository.Add(album);

        // Reload with relationships
        var created = await _repository.GetById(album.AlbumId);

        return MapToResponse(created!);
    }

    public override async Task<AlbumResponse> UpdateAlbum(
        UpdateAlbumRequest request,
        ServerCallContext context)
    {
        var album = await _repository.GetById(request.Id);

        if (album is null)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound,
                $"Album {request.Id} not found"));
        }

        album.Title = request.Title;
        album.ArtistId = request.ArtistId;

        await _repository.Update(album);

        return MapToResponse(album);
    }

    public override async Task<Empty> DeleteAlbum(
        DeleteAlbumRequest request,
        ServerCallContext context)
    {
        var deleted = await _repository.Delete(request.Id);

        if (!deleted)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound,
                $"Album {request.Id} not found"));
        }

        return new Empty();
    }

    private static AlbumResponse MapToResponse(Album album)
    {
        var response = new AlbumResponse
        {
            Id = album.AlbumId,
            Title = album.Title,
            Artist = new ArtistInfo
            {
                Id = album.Artist?.ArtistId ?? 0,
                Name = album.Artist?.Name ?? ""
            }
        };

        if (album.Tracks?.Any() == true)
        {
            response.Tracks.AddRange(album.Tracks.Select(t => new TrackInfo
            {
                Id = t.TrackId,
                Name = t.Name,
                DurationMs = t.Milliseconds
            }));
        }

        return response;
    }
}
```

---

## 5. Register gRPC Service

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add gRPC
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4 MB
    options.MaxSendMessageSize = 4 * 1024 * 1024;
});

// Add gRPC reflection for tools like grpcurl
builder.Services.AddGrpcReflection();

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<AlbumGrpcService>();

// Enable reflection in development
if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

// Can also have REST endpoints alongside gRPC
app.MapGet("/", () => "gRPC + REST API");

app.Run();
```

---

## 6. gRPC Client

### Client Configuration

```csharp
// In a client application or another service
builder.Services.AddGrpcClient<AlbumService.AlbumServiceClient>(options =>
{
    options.Address = new Uri("https://localhost:5001");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    // For development with self-signed certs
    handler.ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});
```

### Using the Client

```csharp
// Services/AlbumClientService.cs
public class AlbumClientService
{
    private readonly AlbumService.AlbumServiceClient _client;

    public AlbumClientService(AlbumService.AlbumServiceClient client)
    {
        _client = client;
    }

    // Unary call
    public async Task<AlbumResponse> GetAlbumAsync(int id, CancellationToken ct)
    {
        try
        {
            return await _client.GetAlbumAsync(
                new GetAlbumRequest { Id = id },
                cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw new NotFoundException($"Album {id} not found");
        }
    }

    // Server streaming
    public async IAsyncEnumerable<AlbumResponse> ListAlbumsAsync(
        string? search = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new ListAlbumsRequest
        {
            PageSize = 100
        };

        if (!string.IsNullOrEmpty(search))
        {
            request.Search = search;
        }

        using var call = _client.ListAlbums(request, cancellationToken: ct);

        await foreach (var album in call.ResponseStream.ReadAllAsync(ct))
        {
            yield return album;
        }
    }

    // Client streaming
    public async Task<BatchCreateResponse> BatchCreateAsync(
        IEnumerable<CreateAlbumRequest> albums,
        CancellationToken ct)
    {
        using var call = _client.BatchCreateAlbums(cancellationToken: ct);

        foreach (var album in albums)
        {
            await call.RequestStream.WriteAsync(album);
        }

        await call.RequestStream.CompleteAsync();

        return await call.ResponseAsync;
    }
}
```

---

## 7. Authentication

### Server-Side JWT Validation

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<AlbumGrpcService>().RequireAuthorization();
```

### Service with Authorization

```csharp
public class AlbumGrpcService : AlbumService.AlbumServiceBase
{
    [Authorize]
    public override async Task<AlbumResponse> CreateAlbum(
        CreateAlbumRequest request,
        ServerCallContext context)
    {
        var user = context.GetHttpContext().User;
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _logger.LogInformation("User {UserId} creating album", userId);

        // ... implementation
    }

    [Authorize(Roles = "Admin")]
    public override async Task<Empty> DeleteAlbum(
        DeleteAlbumRequest request,
        ServerCallContext context)
    {
        // Only admins can delete
    }
}
```

### Client with Authentication

```csharp
builder.Services.AddGrpcClient<AlbumService.AlbumServiceClient>(options =>
{
    options.Address = new Uri("https://localhost:5001");
})
.AddCallCredentials(async (context, metadata) =>
{
    var token = await GetAccessTokenAsync();
    metadata.Add("Authorization", $"Bearer {token}");
});
```

---

## 8. Error Handling

### Custom Exception Interceptor

```csharp
// Interceptors/ExceptionInterceptor.cs
public class ExceptionInterceptor : Interceptor
{
    private readonly ILogger<ExceptionInterceptor> _logger;

    public ExceptionInterceptor(ILogger<ExceptionInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error");
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found");
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access");
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            throw new RpcException(new Status(StatusCode.Internal, "An error occurred"));
        }
    }
}

// Registration
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<ExceptionInterceptor>();
});
```

### Status Codes Mapping

| Exception | gRPC Status Code |
|-----------|-----------------|
| Validation error | `InvalidArgument` |
| Not found | `NotFound` |
| Unauthorized | `Unauthenticated` |
| Forbidden | `PermissionDenied` |
| Conflict | `AlreadyExists` |
| Timeout | `DeadlineExceeded` |
| Rate limited | `ResourceExhausted` |
| Internal error | `Internal` |

---

## 9. Testing gRPC Services

```csharp
public class AlbumGrpcServiceTests
{
    [Fact]
    public async Task GetAlbum_ReturnsAlbum_WhenExists()
    {
        // Arrange
        var repository = new Mock<IAlbumRepository>();
        repository
            .Setup(r => r.GetById(1))
            .ReturnsAsync(new Album
            {
                AlbumId = 1,
                Title = "Test Album",
                Artist = new Artist { ArtistId = 1, Name = "Test Artist" }
            });

        var service = new AlbumGrpcService(
            repository.Object,
            Mock.Of<ILogger<AlbumGrpcService>>());

        var context = TestServerCallContext.Create();

        // Act
        var result = await service.GetAlbum(
            new GetAlbumRequest { Id = 1 },
            context);

        // Assert
        Assert.Equal(1, result.Id);
        Assert.Equal("Test Album", result.Title);
    }

    [Fact]
    public async Task GetAlbum_ThrowsNotFound_WhenMissing()
    {
        // Arrange
        var repository = new Mock<IAlbumRepository>();
        repository.Setup(r => r.GetById(99)).ReturnsAsync((Album?)null);

        var service = new AlbumGrpcService(
            repository.Object,
            Mock.Of<ILogger<AlbumGrpcService>>());

        var context = TestServerCallContext.Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RpcException>(
            () => service.GetAlbum(new GetAlbumRequest { Id = 99 }, context));

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }
}

// Test helper
public static class TestServerCallContext
{
    public static ServerCallContext Create()
    {
        return new TestContext();
    }

    private class TestContext : ServerCallContext
    {
        protected override string MethodCore => "TestMethod";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test-peer";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => new();
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore =>
            new(null, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(
            ContextPropagationOptions? options) => throw new NotImplementedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
            Task.CompletedTask;
    }
}
```

---

## 10. gRPC-Web for Browsers

```csharp
// Enable gRPC-Web for browser clients
builder.Services.AddGrpc();
builder.Services.AddGrpcWeb();

var app = builder.Build();

app.UseGrpcWeb();

app.MapGrpcService<AlbumGrpcService>().EnableGrpcWeb();
```

---

## Summary

### gRPC Patterns

| Pattern | Use Case |
|---------|----------|
| Unary | Simple request/response |
| Server streaming | Large datasets, real-time updates |
| Client streaming | Batch uploads |
| Bidirectional | Real-time sync, chat |

### Best Practices

1. **Use meaningful status codes** — Map exceptions appropriately
2. **Version your proto files** — Plan for evolution
3. **Add interceptors** — For logging, auth, error handling
4. **Stream large data** — Don't load everything into memory
5. **Use deadlines** — Set client-side timeouts
6. **Enable reflection** — For debugging with tools like grpcurl

### File Structure

```
src/
├── Protos/
│   ├── album.proto
│   ├── artist.proto
│   └── common.proto
├── Services/
│   └── AlbumGrpcService.cs
└── Interceptors/
    └── ExceptionInterceptor.cs
```
