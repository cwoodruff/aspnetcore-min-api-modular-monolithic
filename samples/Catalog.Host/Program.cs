using Catalog.Host;
using Catalog.Modules;
using Microsoft.AspNetCore.Http.Json;
using SharedKernel;

// Catalog as its own process: what HostComposition gives every module, Catalog's registrations and
// endpoints, tokens checked against the monolith's published keys, and InvoiceFinalized read from Orders'
// outbox by cursor (ADR-0017). Nothing else from the monolith.
var builder = WebApplication.CreateBuilder(args);
var catalog = new CatalogModule.Modules();

builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.PropertyNamingPolicy = null);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddReflectionJsonSerialization();
builder.Services.AddRemoteIdentity(builder.Configuration);
catalog.RegisterServices(builder.Services, builder.Configuration);
builder.Services.AddOrdersOutboxFeed();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

catalog.MapEndpoints(app);
app.MapHealthChecks("/healthz");

await app.RunAsync();
