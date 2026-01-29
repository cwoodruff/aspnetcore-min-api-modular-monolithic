# SignalR Real-Time Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

SignalR enables real-time bidirectional communication between server and
clients. It's perfect for notifications, live updates, chat features, and
collaborative applications.

**Duration:** 45-60 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of WebSockets

---

## Learning Objectives

By the end of this guide, you will:

- Understand SignalR communication patterns
- Create hubs and handle connections
- Implement authentication for real-time connections
- Broadcast updates from API endpoints
- Scale SignalR with Redis backplane

---

## 1. When to Use SignalR

### Use Cases

| Feature               | Traditional Polling | SignalR           |
|-----------------------|---------------------|-------------------|
| Notifications         | Every 30s request   | Instant push      |
| Live dashboards       | Stale data          | Real-time updates |
| Chat                  | Delayed messages    | Instant delivery  |
| Collaborative editing | Conflicts           | Sync in real-time |

### Transport Protocols

SignalR automatically negotiates the best transport:

1. **WebSockets** — Best performance, bidirectional
2. **Server-Sent Events** — Good fallback, server→client only
3. **Long Polling** — Works everywhere, higher latency

---

## 2. Basic Setup

### Install Package

```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.0.0" />
```

### Configure SignalR

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// Map hub endpoint
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
```

---

## 3. Creating a Hub

### Basic Hub

```csharp
// Hubs/NotificationHub.cs
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client connected: {ConnectionId}",
            Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Client disconnected: {ConnectionId}",
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    // Client can call this method
    public async Task SendMessage(string message)
    {
        _logger.LogInformation("Received: {Message}", message);

        // Broadcast to all clients
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}
```

### Typed Hub

```csharp
// Hubs/INotificationClient.cs
public interface INotificationClient
{
    Task ReceiveNotification(NotificationMessage notification);
    Task ReceiveAlbumUpdate(AlbumUpdateMessage update);
    Task ReceiveOrderStatus(OrderStatusMessage status);
}

public record NotificationMessage(
    string Type,
    string Title,
    string Message,
    DateTime Timestamp);

public record AlbumUpdateMessage(
    int AlbumId,
    string Action,
    string Title);

public record OrderStatusMessage(
    int OrderId,
    string Status,
    DateTime UpdatedAt);

// Hubs/NotificationHub.cs
public class NotificationHub : Hub<INotificationClient>
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public async Task SubscribeToAlbumUpdates()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "album-updates");
        _logger.LogInformation("Client {Id} subscribed to album updates", Context.ConnectionId);
    }

    public async Task UnsubscribeFromAlbumUpdates()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "album-updates");
    }

    public async Task SubscribeToOrder(int orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderId}");
    }
}
```

---

## 4. Authentication

### Configure JWT Authentication for SignalR

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Standard JWT configuration
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // SignalR sends token in query string for WebSocket connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
```

### Authenticated Hub

```csharp
[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            // Add to user-specific group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }
}
```

---

## 5. Sending Messages from API Endpoints

### Inject Hub Context

```csharp
// Services/NotificationService.cs
public interface INotificationService
{
    Task SendToAllAsync(NotificationMessage notification, CancellationToken ct = default);
    Task SendToUserAsync(string userId, NotificationMessage notification, CancellationToken ct = default);
    Task SendAlbumUpdateAsync(AlbumUpdateMessage update, CancellationToken ct = default);
}

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;

    public NotificationService(IHubContext<NotificationHub, INotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendToAllAsync(NotificationMessage notification, CancellationToken ct = default)
    {
        await _hubContext.Clients.All.ReceiveNotification(notification);
    }

    public async Task SendToUserAsync(
        string userId,
        NotificationMessage notification,
        CancellationToken ct = default)
    {
        await _hubContext.Clients
            .Group($"user-{userId}")
            .ReceiveNotification(notification);
    }

    public async Task SendAlbumUpdateAsync(AlbumUpdateMessage update, CancellationToken ct = default)
    {
        await _hubContext.Clients
            .Group("album-updates")
            .ReceiveAlbumUpdate(update);
    }
}

// Registration
builder.Services.AddScoped<INotificationService, NotificationService>();
```

### Use in Endpoints

```csharp
app.MapPost("/api/albums", async (
    CreateAlbumRequest request,
    IAlbumService albumService,
    INotificationService notificationService,
    CancellationToken ct) =>
{
    var album = await albumService.CreateAsync(request, ct);

    // Notify connected clients
    await notificationService.SendAlbumUpdateAsync(
        new AlbumUpdateMessage(album.Id, "created", album.Title),
        ct);

    return TypedResults.Created($"/api/albums/{album.Id}", album);
});

app.MapPut("/api/orders/{id}/status", async (
    int id,
    OrderStatusUpdate update,
    IOrderService orderService,
    IHubContext<NotificationHub, INotificationClient> hubContext,
    CancellationToken ct) =>
{
    var order = await orderService.UpdateStatusAsync(id, update.Status, ct);

    // Notify clients watching this order
    await hubContext.Clients
        .Group($"order-{id}")
        .ReceiveOrderStatus(new OrderStatusMessage(id, update.Status, DateTime.UtcNow));

    return TypedResults.Ok(order);
});
```

---

## 6. Client Connection (JavaScript)

### Basic Connection

```javascript
// npm install @microsoft/signalr

import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/notifications')
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Handle incoming messages
connection.on('ReceiveNotification', (notification) => {
    console.log('Notification:', notification);
    // Update UI
});

connection.on('ReceiveAlbumUpdate', (update) => {
    console.log('Album update:', update);
    // Refresh album list
});

// Start connection
async function start() {
    try {
        await connection.start();
        console.log('SignalR Connected');

        // Subscribe to updates
        await connection.invoke('SubscribeToAlbumUpdates');
    } catch (err) {
        console.error('SignalR Error:', err);
        setTimeout(start, 5000);
    }
}

connection.onclose(async () => {
    await start();
});

start();
```

### Authenticated Connection

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/notifications', {
        accessTokenFactory: () => {
            return localStorage.getItem('accessToken');
        }
    })
    .withAutomaticReconnect()
    .build();
```

---

## 7. Groups and Targeting

### Dynamic Group Management

```csharp
public class CollaborationHub : Hub<ICollaborationClient>
{
    // Join a document editing session
    public async Task JoinDocument(string documentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"doc-{documentId}");

        // Notify others
        await Clients.Group($"doc-{documentId}")
            .UserJoined(Context.User?.Identity?.Name ?? "Anonymous");
    }

    public async Task LeaveDocument(string documentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"doc-{documentId}");

        await Clients.Group($"doc-{documentId}")
            .UserLeft(Context.User?.Identity?.Name ?? "Anonymous");
    }

    // Send cursor position to others in document
    public async Task UpdateCursor(string documentId, CursorPosition position)
    {
        await Clients.OthersInGroup($"doc-{documentId}")
            .CursorMoved(Context.User?.Identity?.Name ?? "Anonymous", position);
    }
}
```

### Targeting Specific Clients

```csharp
// All connected clients
await Clients.All.ReceiveNotification(notification);

// Specific connection
await Clients.Client(connectionId).ReceiveNotification(notification);

// Multiple connections
await Clients.Clients(connectionIds).ReceiveNotification(notification);

// Group
await Clients.Group("admins").ReceiveNotification(notification);

// Multiple groups
await Clients.Groups(new[] { "admins", "moderators" }).ReceiveNotification(notification);

// Everyone except caller
await Clients.Others.ReceiveNotification(notification);

// Everyone in group except caller
await Clients.OthersInGroup("room").ReceiveNotification(notification);

// User by identity
await Clients.User(userId).ReceiveNotification(notification);
```

---

## 8. Scaling with Redis Backplane

### Configure Redis Backplane

```csharp
// For multi-server deployments
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!,
        options =>
        {
            options.Configuration.ChannelPrefix = "ModularMonolith";
        });
```

### Docker Compose

```yaml
version: '3.8'

services:
  api1:
    build: .
    ports:
      - "5043:8080"
    environment:
      - ConnectionStrings__Redis=redis:6379

  api2:
    build: .
    ports:
      - "5044:8080"
    environment:
      - ConnectionStrings__Redis=redis:6379

  redis:
    image: redis:alpine
    ports:
      - "6379:6379"

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
```

### Nginx Configuration for WebSocket

```nginx
upstream api {
    ip_hash;  # Sticky sessions for SignalR
    server api1:8080;
    server api2:8080;
}

server {
    listen 80;

    location / {
        proxy_pass http://api;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

---

## 9. Connection Management

### Track Connected Users

```csharp
// Services/ConnectionTracker.cs
public interface IConnectionTracker
{
    void AddConnection(string userId, string connectionId);
    void RemoveConnection(string userId, string connectionId);
    IEnumerable<string> GetConnections(string userId);
    bool IsUserOnline(string userId);
}

public class ConnectionTracker : IConnectionTracker
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();

    public void AddConnection(string userId, string connectionId)
    {
        _connections.AddOrUpdate(
            userId,
            new HashSet<string> { connectionId },
            (_, existing) =>
            {
                existing.Add(connectionId);
                return existing;
            });
    }

    public void RemoveConnection(string userId, string connectionId)
    {
        if (_connections.TryGetValue(userId, out var connections))
        {
            connections.Remove(connectionId);
            if (connections.Count == 0)
            {
                _connections.TryRemove(userId, out _);
            }
        }
    }

    public IEnumerable<string> GetConnections(string userId)
    {
        return _connections.TryGetValue(userId, out var connections)
            ? connections
            : Enumerable.Empty<string>();
    }

    public bool IsUserOnline(string userId)
    {
        return _connections.ContainsKey(userId);
    }
}

// Registration (singleton for in-memory, or use Redis for distributed)
builder.Services.AddSingleton<IConnectionTracker, ConnectionTracker>();
```

### Use in Hub

```csharp
[Authorize]
public class PresenceHub : Hub<IPresenceClient>
{
    private readonly IConnectionTracker _tracker;

    public PresenceHub(IConnectionTracker tracker)
    {
        _tracker = tracker;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var wasOffline = !_tracker.IsUserOnline(userId);

        _tracker.AddConnection(userId, Context.ConnectionId);

        if (wasOffline)
        {
            // Notify others user came online
            await Clients.Others.UserOnline(userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        _tracker.RemoveConnection(userId, Context.ConnectionId);

        if (!_tracker.IsUserOnline(userId))
        {
            // Notify others user went offline
            await Clients.Others.UserOffline(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
```

---

## 10. Streaming

### Server-to-Client Streaming

```csharp
public class StreamHub : Hub
{
    // Stream data to client
    public async IAsyncEnumerable<int> Counter(
        int count,
        int delay,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return i;
            await Task.Delay(delay, ct);
        }
    }

    // Stream large dataset
    public async IAsyncEnumerable<AlbumDto> StreamAlbums(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var albums = GetAlbumsEnumerable(); // Returns IAsyncEnumerable<Album>

        await foreach (var album in albums.WithCancellation(ct))
        {
            yield return MapToDto(album);
        }
    }
}
```

### Client Consumption (JavaScript)

```javascript
// Start streaming
const stream = connection.stream('Counter', 10, 500);

stream.subscribe({
    next: (item) => console.log('Received:', item),
    complete: () => console.log('Stream completed'),
    error: (err) => console.error('Stream error:', err)
});

// Cancel stream
// stream.dispose();
```

---

## 11. Testing

```csharp
public class NotificationHubTests
{
    [Fact]
    public async Task OnConnected_AddsUserToGroup()
    {
        // Arrange
        var mockClients = new Mock<IHubClients<INotificationClient>>();
        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();

        mockContext.Setup(c => c.ConnectionId).Returns("test-connection");
        mockContext.Setup(c => c.User).Returns(CreateTestUser("user-123"));

        var hub = new NotificationHub(Mock.Of<ILogger<NotificationHub>>())
        {
            Clients = mockClients.Object,
            Groups = mockGroups.Object,
            Context = mockContext.Object
        };

        // Act
        await hub.OnConnectedAsync();

        // Assert
        mockGroups.Verify(g => g.AddToGroupAsync(
            "test-connection",
            "user-user-123",
            default),
            Times.Once);
    }

    private static ClaimsPrincipal CreateTestUser(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
```

---

## Summary

### Hub Methods Comparison

| Pattern               | Use Case                    |
|-----------------------|-----------------------------|
| `Clients.All`         | Broadcast to everyone       |
| `Clients.Group(name)` | Feature-specific updates    |
| `Clients.User(id)`    | User-specific notifications |
| `Clients.Others`      | Exclude sender              |

### Best Practices

1. **Use typed hubs** — Compile-time safety for messages
2. **Authenticate connections** — Pass token in query string
3. **Use groups wisely** — Don't create too many small groups
4. **Handle reconnection** — Clients should auto-reconnect
5. **Scale with backplane** — Redis for multiple servers
6. **Track connections** — For presence features

### Integration Points

```csharp
// From endpoint
await hubContext.Clients.All.ReceiveNotification(notification);

// From background service
await hubContext.Clients.Group("admin").ReceiveAlert(alert);

// From domain event handler
await hubContext.Clients.User(userId).ReceiveUpdate(update);
```
