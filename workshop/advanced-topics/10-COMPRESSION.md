# Request/Response Compression Implementation Guide

## ASP.NET Core Minimal API Advanced Topics

---

## Overview

Response compression reduces the size of HTTP responses, improving performance
and reducing bandwidth costs. ASP.NET Core supports Brotli and Gzip compression
out of the box.

**Duration:** 20-30 minutes
**Prerequisites:** Basic Minimal API knowledge, understanding of HTTP headers

---

## Learning Objectives

By the end of this guide, you will:

- Configure response compression middleware
- Understand Brotli vs Gzip trade-offs
- Implement compression for different content types
- Handle compression in specific scenarios
- Monitor compression effectiveness

---

## 1. Why Compression Matters

### Impact on Performance

| Metric                | Without Compression | With Compression      |
|-----------------------|---------------------|-----------------------|
| JSON Response (100KB) | 100KB               | ~15KB (85% reduction) |
| HTML Page (50KB)      | 50KB                | ~8KB (84% reduction)  |
| Network Time (3G)     | 800ms               | 120ms                 |
| Bandwidth Cost        | $100/month          | $15/month             |

### Compression Algorithms

| Algorithm   | Compression Ratio | Speed  | Browser Support |
|-------------|-------------------|--------|-----------------|
| **Brotli**  | Excellent         | Slower | Modern browsers |
| **Gzip**    | Good              | Fast   | All browsers    |
| **Deflate** | Good              | Fast   | All browsers    |

---

## 2. Basic Configuration

### Enable Response Compression

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Enable for HTTPS (consider security)
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

var app = builder.Build();

// Use compression middleware (should be early in pipeline)
app.UseResponseCompression();

// Other middleware...
app.UseRouting();
app.UseAuthorization();

app.Run();
```

### Default Behavior

By default, compression is enabled for:

- `text/plain`
- `text/css`
- `application/javascript`
- `text/html`
- `application/xml`
- `text/xml`
- `application/json`
- `text/json`

---

## 3. Configuring Compression Providers

### Brotli Configuration

```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    // Quality levels: 0-11 (higher = better compression, slower)
    // Recommended: 4-6 for web (balance of size and speed)
    options.Level = CompressionLevel.Optimal; // Maps to quality 4
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});
```

### Compression Levels

```csharp
// CompressionLevel options
CompressionLevel.Fastest       // Quick compression, larger output
CompressionLevel.Optimal       // Balanced (recommended)
CompressionLevel.NoCompression // Disabled
CompressionLevel.SmallestSize  // Best compression, slower

// For Brotli specifically
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    // Direct quality setting (0-11)
    // 0 = fastest, 11 = smallest size
    // Web recommendation: 4-6
    options.Level = (CompressionLevel)5;
});
```

---

## 4. Adding Custom MIME Types

### Configure Additional Types

```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();

    // Add custom MIME types to compress
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/xml",
        "text/csv",
        "application/octet-stream",
        "image/svg+xml",
        "application/vnd.api+json",
        "application/hal+json",
        "application/problem+json"
    });
});
```

### Exclude Specific Types

```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;

    // Exclude already-compressed content
    options.ExcludedMimeTypes = new[]
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
        "application/zip",
        "application/pdf"
    };
});
```

---

## 5. Conditional Compression

### Skip Compression for Small Responses

```csharp
// Custom compression provider that skips small responses
public class SmartCompressionProvider : ICompressionProvider
{
    private readonly BrotliCompressionProvider _brotli;
    private const int MinSizeToCompress = 1024; // 1KB minimum

    public SmartCompressionProvider()
    {
        _brotli = new BrotliCompressionProvider(Options.Create(
            new BrotliCompressionProviderOptions
            {
                Level = CompressionLevel.Optimal
            }));
    }

    public string EncodingName => "br";
    public bool SupportsFlush => true;

    public Stream CreateStream(Stream outputStream)
    {
        return _brotli.CreateStream(outputStream);
    }
}
```

### Disable Compression per Endpoint

```csharp
app.MapGet("/api/small-data", () =>
{
    // Small response, compression overhead not worth it
    return new { status = "ok" };
})
.WithMetadata(new DisableResponseCompressionAttribute());

// Or using extension method
public static class CompressionExtensions
{
    public static RouteHandlerBuilder DisableCompression(this RouteHandlerBuilder builder)
    {
        return builder.WithMetadata(new DisableResponseCompressionAttribute());
    }
}

app.MapGet("/api/realtime", StreamData)
    .DisableCompression(); // Disable for streaming
```

---

## 6. HTTPS Security Considerations

### The CRIME/BREACH Vulnerability

Compression over HTTPS can leak information when:

1. Attacker can inject content into request
2. Response includes user secrets + attacker content
3. Compressed size reveals information

### Mitigation Strategies

```csharp
builder.Services.AddResponseCompression(options =>
{
    // Option 1: Disable for HTTPS (safest but impacts performance)
    options.EnableForHttps = false;

    // Option 2: Enable for HTTPS (common for APIs)
    options.EnableForHttps = true;
});

// If enabling for HTTPS, also implement:
// 1. CSRF protection for state-changing requests
// 2. SameSite cookies
// 3. Rate limiting

// For sensitive responses, disable compression
app.MapGet("/api/user/profile", GetUserProfile)
    .DisableCompression() // Don't compress sensitive data
    .RequireAuthorization();
```

---

## 7. Request Decompression

### Handle Compressed Requests

```csharp
// Add request decompression (for clients sending compressed bodies)
builder.Services.AddRequestDecompression();

var app = builder.Build();

app.UseRequestDecompression();
app.UseResponseCompression();
```

### Configure Request Decompression

```csharp
builder.Services.AddRequestDecompression(options =>
{
    // Add custom decompression providers
    options.DecompressionProviders.Add("br", new BrotliDecompressionProvider());
    options.DecompressionProviders.Add("gzip", new GzipDecompressionProvider());
});
```

---

## 8. Monitoring Compression

### Middleware to Log Compression Stats

```csharp
// Middleware/CompressionLoggingMiddleware.cs
public class CompressionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CompressionLoggingMiddleware> _logger;

    public CompressionLoggingMiddleware(
        RequestDelegate next,
        ILogger<CompressionLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Capture original response body
        var originalBodyStream = context.Response.Body;

        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await _next(context);

        var originalSize = memoryStream.Length;
        var encoding = context.Response.Headers.ContentEncoding.ToString();

        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(originalBodyStream);

        if (!string.IsNullOrEmpty(encoding))
        {
            _logger.LogInformation(
                "Response compressed: {Path} - Original: {OriginalSize} bytes, Encoding: {Encoding}",
                context.Request.Path,
                originalSize,
                encoding);
        }
    }
}
```

### Response Headers

```http
HTTP/1.1 200 OK
Content-Type: application/json
Content-Encoding: br
Vary: Accept-Encoding
```

---

## 9. Testing Compression

```csharp
public class CompressionTests
{
    [Fact]
    public async Task Response_IsCompressed_WhenClientSupportsIt()
    {
        // Arrange
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();
        client.DefaultRequestHeaders.AcceptEncoding.Add(
            new StringWithQualityHeaderValue("br"));
        client.DefaultRequestHeaders.AcceptEncoding.Add(
            new StringWithQualityHeaderValue("gzip"));

        // Act
        var response = await client.GetAsync("/api/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            response.Content.Headers.ContentEncoding,
            e => e == "br" || e == "gzip");
    }

    [Fact]
    public async Task Response_NotCompressed_WhenClientDoesNotSupportIt()
    {
        // Arrange
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();
        // Don't add Accept-Encoding header

        // Act
        var response = await client.GetAsync("/api/albums");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task CompressionRatio_IsSignificant_ForJsonResponses()
    {
        // Arrange
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        // Get uncompressed size
        var uncompressedResponse = await client.GetAsync("/api/albums");
        var uncompressedContent = await uncompressedResponse.Content.ReadAsByteArrayAsync();

        // Get compressed size
        client.DefaultRequestHeaders.AcceptEncoding.Add(
            new StringWithQualityHeaderValue("br"));
        var compressedResponse = await client.GetAsync("/api/albums");
        var compressedContent = await compressedResponse.Content.ReadAsByteArrayAsync();

        // Assert
        var compressionRatio = (double)compressedContent.Length / uncompressedContent.Length;
        Assert.True(compressionRatio < 0.5, "Expected at least 50% compression");
    }
}
```

---

## 10. Complete Configuration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;

    // Brotli first (better compression), Gzip as fallback
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();

    // MIME types to compress
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/problem+json",
        "text/csv",
        "image/svg+xml"
    });
});

// Configure Brotli for web (quality 5 = good balance)
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

// Configure Gzip
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

// Add request decompression for compressed request bodies
builder.Services.AddRequestDecompression();

var app = builder.Build();

// Middleware order matters!
app.UseRequestDecompression();  // First: decompress incoming
app.UseResponseCompression();   // Second: compress outgoing

// Rest of middleware...
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapGet("/api/albums", GetAlbums);

// Disable compression for specific endpoints
app.MapGet("/api/stream", StreamData)
    .WithMetadata(new DisableResponseCompressionAttribute());

app.Run();
```

---

## Summary

### Configuration Quick Reference

| Setting        | Recommendation                                            |
|----------------|-----------------------------------------------------------|
| EnableForHttps | `true` for APIs, consider `false` for sensitive web pages |
| Brotli Level   | `Optimal` (quality 4-5)                                   |
| Gzip Level     | `Optimal`                                                 |
| Minimum Size   | Don't compress responses < 1KB                            |

### Best Practices

1. **Use Brotli first** — Better compression than Gzip
2. **Enable for HTTPS** — Performance gains usually outweigh risks for APIs
3. **Skip small responses** — Overhead not worth it under 1KB
4. **Exclude binary types** — Already compressed (images, PDFs)
5. **Test compression ratios** — Verify actual savings
6. **Monitor bandwidth** — Track before/after metrics

### Response Headers

```http
# Request
Accept-Encoding: br, gzip, deflate

# Response (compressed)
Content-Encoding: br
Vary: Accept-Encoding
```

### Middleware Order

```csharp
app.UseRequestDecompression();  // 1st - Handle compressed requests
app.UseResponseCompression();   // 2nd - Compress responses
app.UseStaticFiles();           // Static file serving
app.UseRouting();               // Routing
app.UseAuthorization();         // Auth
// ...endpoints
```
