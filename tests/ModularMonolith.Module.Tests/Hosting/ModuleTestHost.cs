using FluentValidation;
using Identity.Modules.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Respawn;
using SharedKernel;
using SharedKernel.Events;
using SharedKernel.Persistence;

namespace ModularMonolith.Module.Tests.Hosting;

/// <summary>
///     One module, on its own: a <see cref="WebApplication" /> on <see cref="TestServer" /> that registers what
///     SharedKernel provides and the module's own services and endpoints, against a database holding only the
///     module's schema (Reporting also has the schemas its views read). Other modules are not loaded:
///     Identity's policies are faked by <see cref="TestAuth" />, events are delivered with
///     <see cref="DeliverAsync" />, and published events are recorded.
/// </summary>
public sealed class ModuleTestHost<TModule> : IAsyncDisposable where TModule : IModule, new()
{
    private readonly WebApplication _app;
    private readonly Respawner? _respawner;

    internal ModuleTestHost(WebApplication app, ModuleData data, string connectionString, Respawner? respawner)
    {
        _app = app;
        Data = data;
        ConnectionString = connectionString;
        _respawner = respawner;
    }

    public ModuleData Data { get; }

    public string ConnectionString { get; }

    public IServiceProvider Services => _app.Services;

    /// <summary>A client calling as <paramref name="user" />, or anonymously.</summary>
    public HttpClient CreateClient(TestUser? user = null, string? requestTenant = null)
    {
        var client = _app.GetTestClient();
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuth.UserHeader, user.Name);
            client.DefaultRequestHeaders.Add(TestAuth.PermissionsHeader, string.Join(',', user.Permissions));
            client.DefaultRequestHeaders.Add(TestAuth.RolesHeader, string.Join(',', user.Roles));
            client.DefaultRequestHeaders.Add(TestAuth.TenantHeader, user.Tenant);
            client.DefaultRequestHeaders.Add(TestAuth.RequestTenantHeader, requestTenant ?? user.Tenant);
        }

        return client;
    }

    /// <summary>Empties the module's schema(s), keeping the migration history.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }

    /// <summary>The module's DbContext as its services get it, in a new scope.</summary>
    public AsyncServiceScope CreateScope() => _app.Services.CreateAsyncScope();

    /// <summary>
    ///     Delivers an integration event to this module's handlers, as the publishing module's outbox dispatcher
    ///     would: each handler in its own scope, through the module's inbox. Returns how many handlers ran.
    /// </summary>
    public async Task<int> DeliverAsync(IIntegrationEvent integrationEvent, Guid? eventId = null)
    {
        var ran = 0;
        foreach (var subscription in _app.Services.GetServices<IntegrationEventSubscription>()
                     .Where(s => s.EventType == integrationEvent.GetType()))
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredKeyedService<DbContext>(subscription.ModuleKey);
            if (await InboxGuard.RunAsync(context, eventId ?? integrationEvent.EventId,
                    subscription.HandlerType.FullName!, TimeProvider.System.GetUtcNow(),
                    ct => subscription.Handle(scope.ServiceProvider, integrationEvent, ct), CancellationToken.None))
            {
                ran++;
            }
        }

        return ran;
    }

    /// <summary>Events the module published since the host started.</summary>
    public IReadOnlyList<IIntegrationEvent> Published =>
        _app.Services.GetService<RecordingEventPublisher.Log>()?.Events ?? [];

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

/// <summary>Starts <see cref="ModuleTestHost{TModule}" />s.</summary>
public static class ModuleTestHost
{
    public static async Task<ModuleTestHost<TModule>> StartAsync<TModule>(ModuleData data, bool seeded,
        TimeProvider? time = null, IDictionary<string, string?>? configuration = null)
        where TModule : IModule, new()
    {
        var module = new TModule();
        var connectionString = data.MigratedSchemas.Count > 0
            ? await PostgresServer.CreateDatabaseAsync(data, seeded)
            : "Host=unused;Database=unused";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(TModule).Assembly.GetName().Name
        });
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AppDatabase"] = connectionString,
            ["ConnectionStrings:Reporting"] = PostgresServer.AsReportingReader(connectionString),
            ["Outbox:Enabled"] = "false",
            ["Reporting:Integrity:Enabled"] = "false"
        });
        if (configuration is not null)
        {
            builder.Configuration.AddInMemoryCollection(configuration);
        }

        var services = builder.Services;
        // What the host provides to every module (HostComposition), minus the other modules.
        services.Configure<JsonOptions>(options => options.SerializerOptions.PropertyNamingPolicy = null);
        services.AddProblemDetails();
        services.AddHealthChecks();
        services.AddReflectionJsonSerialization();
        if (time is not null)
        {
            services.AddSingleton(time);
        }

        var isIdentity = module.Name == "Identity";
        if (isIdentity)
        {
            services.AddIdentityAuth(builder.Configuration);
        }
        else
        {
            services.AddTestAuthentication();
        }

        module.RegisterServices(services, builder.Configuration);
        services.TryAddSingleton(TimeProvider.System);

        // Record what the module publishes, while still writing its outbox.
        RecordingEventPublisher.Wrap(services, module.Name);

        var app = builder.Build();
        app.UseExceptionHandler(errors => errors.Run(WriteProblemAsync));
        app.UseRateLimiter();
        if (isIdentity)
        {
            app.UseIdentityAuth();
        }
        else
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        module.MapEndpoints(app);
        await app.StartAsync();

        Respawner? respawner = null;
        if (data.MigratedSchemas.Count > 0)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                SchemasToInclude = [.. data.MigratedSchemas],
                TablesToIgnore = [new Respawn.Graph.Table(ModuleDbContextOptions.HistoryTable)],
                DbAdapter = DbAdapter.Postgres,
                WithReseed = true
            });
        }

        return new ModuleTestHost<TModule>(app, data, connectionString, respawner);
    }

    // The full host's exception mapping in miniature: validation failures are 400s, the rest 500s.
    private static Task WriteProblemAsync(HttpContext context)
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var extensions = new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier };
        return exception is ValidationException validation
            ? Results.ValidationProblem(
                    validation.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                    detail: "One or more validation errors occurred.", title: "Request validation failed.",
                    type: "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1", extensions: extensions)
                .ExecuteAsync(context)
            : Results.Problem(statusCode: 500, title: "An unexpected error occurred.", extensions: extensions).ExecuteAsync(context);
    }
}
