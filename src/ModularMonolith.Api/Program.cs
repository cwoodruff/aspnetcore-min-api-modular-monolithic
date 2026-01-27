using System.Reflection;
using Admin.Modules;
using Identity.Modules;
using Identity.Modules.Extensions;
using Microsoft.AspNetCore.Http.Json;
using Music.Modules;
using Orders.Modules;
using Reporting.Modules;
using SharedKernel;
using SharedKernel.Caching;
using SharedKernel.Persistence;
using SharedKernel.TrafficControl;
using System.Threading.RateLimiting;
using Microsoft.OpenApi;
using SharedKernel.DataSQLite.Repositories;
using SharedKernel.Persistence.Repositories;

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
        Description = "ASP.NET Core Minimal API Modular Monolith with modules: Music, Orders, Administration, Reporting, Identity.",
        Contact = new OpenApiContact { Name = "API Team" }
    });

    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste your JWT access token only (no 'Bearer ' prefix). Swagger will add the prefix automatically.",
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
    static string? TryFindDb(string contentRoot)
    {
        var contentDb = Path.Combine(contentRoot, "data", "chinook.db");
        if (File.Exists(contentDb))
        {
            return contentDb;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "data")))
        {
            current = current.Parent;
        }
        var root = current?.FullName;
        var rootDb = root is not null ? Path.Combine(root, "data", "chinook.db") : null;
        return rootDb is not null && File.Exists(rootDb) ? rootDb : null;
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
            partitionKey: PartitionKeys.FromRequest(context),
            factory: _ => new FixedWindowRateLimiterOptions
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

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Default");

// Rate limiter should run early in the pipeline
app.UseRateLimiter();

// AuthN/AuthZ middleware from Identity module
app.UseIdentityAuth();

app.UseSwagger();
app.UseSwaggerUI();


// Root endpoint with service metadata
app.MapGet("/", (IConfiguration cfg, IWebHostEnvironment env) =>
{
    var response = new
    {
        module = "root",
        status = "Healthy",
        timestampUtc = DateTime.UtcNow.ToString("O"),
        environment = env.EnvironmentName,
        version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                  ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                  ?? "1.0.0",
        service = cfg["ServiceName"] ?? "ModularMonolith.Api",
    };
    return Results.Json(response);
})
.WithName("Root")
.Produces(200)
.WithTags("Root")
.Produces(429) // Rate limiting
.RequireRateLimiting(SharedKernel.TrafficControl.RateLimitPolicyRegistry.Names.GlobalPublicAnon);

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

static string[] GetAllowedOrigins() =>
[
    "http://localhost:3000", "http://localhost:4200", "http://localhost:5173",
    "https://localhost:3000", "https://localhost:4200", "https://localhost:5173"
];


// For WebApplicationFactory
#pragma warning disable ASP0027 // Using partial Program to expose entry point for tests; acceptable in this project
namespace ModularMonolith.Api
{
    public partial class Program { }
}
#pragma warning restore ASP0027
