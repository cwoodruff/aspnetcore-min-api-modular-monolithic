using System.Reflection;
using System.Text.Json;
using FluentValidation;
using Identity.Modules.Extensions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModularMonolith.Api;
using SharedKernel;
using SharedKernel.TrafficControl;

var builder = WebApplication.CreateBuilder(args);

var modules = HostComposition.ConfigureServices(builder);

var app = builder.Build();

if ((app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
    && app.Configuration.GetValue<bool>("Database:MigrateAndSeedOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await DbSeeder.MigrateAndSeedAsync(
        scope.ServiceProvider,
        Path.Combine(AppContext.BaseDirectory, DbSeeder.SeedScriptRelativePath));
}

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

// OWASP A05: Security headers to prevent clickjacking, MIME-sniffing, and XSS.
// Applied in OnStarting so they survive the exception handler's Response.Clear().
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        var headers = ctx.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers.XXSSProtection = "0"; // modern browsers: CSP replaces this
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        return Task.CompletedTask;
    });
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
    .RequireRateLimiting(RateLimitPolicyRegistry.GlobalPublicAnon);

// Per-module health (each module's DbContext check, tagged with the module name)
app.MapHealthChecks("/healthz", new HealthCheckOptions { ResponseWriter = WriteHealthAsync })
    .WithName("HealthZ")
    .WithTags("Root");

// Map module endpoints
app.MapGroup(""); // noop to ensure route builder initialized
foreach (var module in modules)
{
    module.MapEndpoints(app);
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
{
    ModuleComposition.ValidateEndpoints(app);
}

app.Run();

static Task WriteHealthAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        modules = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new
            {
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                tags = entry.Value.Tags
            })
    });
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
