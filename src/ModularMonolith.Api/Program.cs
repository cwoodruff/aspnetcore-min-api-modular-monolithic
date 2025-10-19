using System.Reflection;
using Microsoft.AspNetCore.Http.Json;
using SharedKernel;
using SharedKernel.Persistence;
using Identity.Modules.Extensions;
using SharedKernel.Caching;
using Microsoft.OpenApi.Models;

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
        Description = "Enter 'Bearer' [space] and then your valid JWT.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", jwtSecurityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtSecurityScheme, Array.Empty<string>() }
    });
});
builder.Services.AddProblemDetails();

// EF Core persistence registration (single SQLite file at /data/chinook.db)
var dataPath = Path.Combine(builder.Environment.ContentRootPath, "data", "chinook.db");
Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
var connString = $"Data Source={dataPath}";

builder.Configuration["ConnectionStrings:AppDatabase"] = connString;
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
.WithTags("Root");

// Register and compose modules
var modules = GetModules();

foreach (var module in modules)
{
    module.RegisterServices(builder.Services, app.Configuration); // register into DI if needed (no-op for now)
}

// Map endpoints after building to ensure middleware is in place
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
        new Administration.Modules.AdministrationModule.Modules(),
        new Identity.Modules.IdentityModule.Modules(),
        new Music.Modules.MusicModule.Modules(),
        new Orders.Modules.OrdersModule.Modules(),
        new Reporting.Modules.ReportingModule.Modules()
    ];
}

static string[] GetAllowedOrigins() =>
[
    "http://localhost:3000", "http://localhost:4200", "http://localhost:5173",
    "https://localhost:3000", "https://localhost:4200", "https://localhost:5173"
];


// For WebApplicationFactory
#pragma warning disable ASP0027 // Using partial Program to expose entry point for tests; acceptable in this project
public partial class Program { }
#pragma warning restore ASP0027
