using System.Reflection;
using System.Text.Json;
using System.Threading.RateLimiting;
using Admin.Modules;
using FluentValidation;
using Identity.Modules;
using Identity.Modules.Extensions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi;
using Music.Modules;
using Orders.Modules;
using Reporting.Modules;
using SharedKernel;
using SharedKernel.Caching;
using SharedKernel.DataSQLite.Repositories;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Repositories;
using SharedKernel.TrafficControl;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null; // keep exact casing provided in anonymous objects
});

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Modular Monolith API",
        Version = "v1",
        Description =
            "ASP.NET Core Minimal API Modular Monolith with modules: Music, Orders, Administration, Reporting, Identity.",
        Contact = new OpenApiContact { Name = "API Team" }
    });

    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description =
            "Paste your JWT access token only (no 'Bearer ' prefix). Swagger will add the prefix automatically.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    c.AddSecurityDefinition("Bearer", jwtSecurityScheme);
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        [new OpenApiSecuritySchemeReference("X-API-Key", document)] = []
    });
});
builder.Services.AddProblemDetails();

// EF Core persistence registration
// Resolve SQLite path for AppDbContext if not provided via configuration/environment.
var existing = builder.Configuration.GetConnectionString("AppDatabase")
               ?? builder.Configuration["ConnectionStrings:AppDatabase"]
               ?? Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase");
if (string.IsNullOrWhiteSpace(existing))
{
    static bool HasUsableDb(string path)
    {
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }

    static string? TryFindDb(string contentRoot)
    {
        var contentDb = Path.Combine(contentRoot, "data", "chinook.db");
        if (HasUsableDb(contentDb))
        {
            return contentDb;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !HasUsableDb(Path.Combine(current.FullName, "data", "chinook.db")))
        {
            current = current.Parent;
        }

        var root = current?.FullName;
        var rootDb = root is not null ? Path.Combine(root, "data", "chinook.db") : null;
        return rootDb is not null && HasUsableDb(rootDb) ? rootDb : null;
    }

    var dbPath = TryFindDb(builder.Environment.ContentRootPath);
    if (!string.IsNullOrWhiteSpace(dbPath))
    {
        builder.Configuration["ConnectionStrings:AppDatabase"] = $"Data Source={dbPath}";
    }
}

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

builder.Services.AddKernelPersistence(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy
            .WithOrigins(GetAllowedOrigins())
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Identity Auth registration (lives in Identity module)
builder.Services.AddIdentityAuth(builder.Configuration);

// Central caching registration (L1 IMemoryCache by default; L2 if configured)
builder.Services.AddCentralCaching(builder.Configuration);

// Option A: Minimal in-app rate limiting wiring
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicyRegistry.Names.GlobalPublicAnon, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKeys.FromRequest(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60, // 60 requests per 60 seconds
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Register module services BEFORE building the app
var modules = GetModules();
foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(WriteProblemDetailsResponseAsync);
});
app.UseStatusCodePages();

// OWASP A05: Security headers to prevent clickjacking, MIME-sniffing, and XSS
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers.XFrameOptions = "DENY";
    ctx.Response.Headers.XXSSProtection = "0"; // modern browsers: CSP replaces this
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'";
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseCors("Default");

// Rate limiter should run early in the pipeline
app.UseRateLimiter();

// AuthN/AuthZ middleware from Identity module
app.UseIdentityAuth();

if (BuildInfoProvider.ShouldExposeOperationalMetadata(app.Environment))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// Root endpoint with service metadata
app.MapGet("/", (IConfiguration cfg, IWebHostEnvironment env) =>
    {
        var timestampUtc = DateTime.UtcNow.ToString("O");

        if (!BuildInfoProvider.ShouldExposeOperationalMetadata(env))
        {
            return Results.Json(new
            {
                module = "root",
                status = "Healthy",
                timestampUtc
            });
        }

        var response = new
        {
            module = "root",
            status = "Healthy",
            timestampUtc,
            environment = env.EnvironmentName,
            version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                          ?.InformationalVersion
                      ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                      ?? "1.0.0",
            service = cfg["ServiceName"] ?? "ModularMonolith.Api"
        };
        return Results.Json(response);
    })
    .WithName("Root")
    .Produces(200)
    .WithTags("Root")
    .Produces(429) // Rate limiting
    .RequireRateLimiting(RateLimitPolicyRegistry.Names.GlobalPublicAnon);

// Map module endpoints
app.MapGroup(""); // noop to ensure route builder initialized
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

static string[] GetAllowedOrigins()
{
    return
    [
        "http://localhost:3000", "http://localhost:4200", "http://localhost:5173",
        "https://localhost:3000", "https://localhost:4200", "https://localhost:5173"
    ];
}

static async Task WriteProblemDetailsResponseAsync(HttpContext context)
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalExceptionHandler");
    var extensions = new Dictionary<string, object?>
    {
        ["traceId"] = context.TraceIdentifier
    };

    switch (exception)
    {
        case ValidationException validationException:
            ExceptionHandlerLog.ValidationFailure(
                logger,
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                validationException);

            var errors = validationException.Errors
                .GroupBy(error => string.IsNullOrWhiteSpace(error.PropertyName) ? string.Empty : error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

            if (errors.Count == 0 && !string.IsNullOrWhiteSpace(validationException.Message))
            {
                errors[string.Empty] = [validationException.Message];
            }

            await Results.ValidationProblem(
                    errors,
                    detail: "One or more validation errors occurred.",
                    title: "Request validation failed.",
                    type: "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
                    extensions: extensions)
                .ExecuteAsync(context);
            return;

        case BadHttpRequestException badHttpRequestException:
            ExceptionHandlerLog.BadRequest(
                logger,
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                badHttpRequestException);
            await Results.Problem(
                    statusCode: badHttpRequestException.StatusCode,
                    title: "Malformed request.",
                    detail: badHttpRequestException.Message,
                    type: "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
                    extensions: extensions)
                .ExecuteAsync(context);
            return;

        case JsonException jsonException:
            ExceptionHandlerLog.JsonParsingFailure(
                logger,
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                jsonException);
            await Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Malformed request.",
                    detail: jsonException.Message,
                    type: "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
                    extensions: extensions)
                .ExecuteAsync(context);
            return;

        case null:
            ExceptionHandlerLog.ExceptionHandlerMissingException(
                logger,
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty);
            break;

        default:
            ExceptionHandlerLog.UnhandledException(
                logger,
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                exception);
            break;
    }

    await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred.",
            type: "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
            extensions: extensions)
        .ExecuteAsync(context);
}

static partial class ExceptionHandlerLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Validation failure for {Method} {Path}")]
    public static partial void ValidationFailure(ILogger logger, string method, string path, Exception exception);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Bad request for {Method} {Path}")]
    public static partial void BadRequest(ILogger logger, string method, string path, Exception exception);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "JSON parsing failure for {Method} {Path}")]
    public static partial void JsonParsingFailure(ILogger logger, string method, string path, Exception exception);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Error,
        Message = "Unhandled exception handler invoked without an exception for {Method} {Path}")]
    public static partial void ExceptionHandlerMissingException(ILogger logger, string method, string path);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    public static partial void UnhandledException(ILogger logger, string method, string path, Exception exception);
}


// For WebApplicationFactory
#pragma warning disable ASP0027 // Using partial Program to expose entry point for tests; acceptable in this project
namespace ModularMonolith.Api
{
    public class Program
    {
    }
}
#pragma warning restore ASP0027
