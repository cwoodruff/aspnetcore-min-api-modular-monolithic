using System.Reflection;
using Microsoft.AspNetCore.Http.Json;
using SharedKernel;
using SharedKernel.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null; // keep exact casing provided in anonymous objects
});

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:4200",
                "http://localhost:5173",
                "https://localhost:3000",
                "https://localhost:4200",
                "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

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

app.UseSwagger();
app.UseSwaggerUI();

// Ensure database is created in Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

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

// Discover and compose modules
// Force-load module assemblies so reflection-based discovery works reliably in all hosting contexts
_ = new[]
{
    typeof(Music.Modules.MusicModule).Assembly,
    typeof(Orders.Modules.OrdersModule).Assembly,
    typeof(Administration.Modules.AdministrationModule).Assembly,
    typeof(Reporting.Modules.ReportingModule).Assembly,
    typeof(Identity.Modules.IdentityModule).Assembly,
};
var modules = DiscoverModules();

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

static IReadOnlyList<IModule> DiscoverModules()
{
    var result = new List<IModule>();

    // Load all referenced assemblies to ensure modules are discovered
    var entry = Assembly.GetEntryAssembly();
    if (entry is not null)
    {
        foreach (var name in entry.GetReferencedAssemblies())
        {
            try { _ = Assembly.Load(name); } catch { /* ignore */ }
        }
    }

    // Also include already loaded assemblies
    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

    foreach (var asm in assemblies)
    {
        try
        {
            var moduleTypes = asm.GetTypes()
                .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .ToArray();

            foreach (var type in moduleTypes)
            {
                if (Activator.CreateInstance(type) is IModule instance)
                {
                    result.Add(instance);
                }
            }
        }
        catch
        {
            // ignore any type load exceptions
        }
    }

    // Ensure deterministic order by name
    return result
        .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

// For WebApplicationFactory
#pragma warning disable ASP0027 // Using partial Program to expose entry point for tests; acceptable in this project
public partial class Program { }
#pragma warning restore ASP0027
