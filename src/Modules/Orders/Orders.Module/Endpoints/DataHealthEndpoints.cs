using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Orders.Modules.Data;
using SharedKernel;

namespace Orders.Modules.Endpoints;

internal static class OrdersDataHealthEndpoints
{
    public static void MapOrdersDataHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/data-health", OrdersDataHealthHandlers.DataHealth)
            .WithName("OrdersDataHealth")
            .Produces(200)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy
    }
}

/// <summary>Operational endpoint handlers. They return <see cref="IResult" />: the body is minimal or detailed
/// depending on the environment (BuildInfoProvider), so there is no single typed shape.</summary>
internal static class OrdersDataHealthHandlers
{
    public static async Task<IResult> DataHealth(OrdersDbContext db, IHostEnvironment env, IConfiguration cfg, CancellationToken ct)
    {
        bool canConnect;
        try
        {
            canConnect = await db.Database.CanConnectAsync(ct);
        }
        catch
        {
            canConnect = false;
        }

        var timestampUtc = DateTime.UtcNow.ToString("O");
        var status = canConnect ? "Data-Healthy" : "Degraded";

        if (!BuildInfoProvider.ShouldExposeOperationalMetadata(env))
        {
            return Results.Json(new
            {
                module = "Orders",
                status,
                timestampUtc
            });
        }

        var response = new
        {
            module = "Orders",
            status,
            timestampUtc,
            environment = BuildInfoProvider.GetEnvironment(env),
            version = BuildInfoProvider.GetInformationalVersion(typeof(OrdersModule).Assembly),
            service = BuildInfoProvider.GetServiceName(cfg),
            database = new { connected = canConnect }
        };
        return Results.Json(response);
    }
}
