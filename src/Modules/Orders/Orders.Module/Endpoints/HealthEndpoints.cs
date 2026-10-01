using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace Orders.Modules.Endpoints;

internal static class OrdersHealthEndpoints
{
    public static void MapOrdersHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/health", OrdersHealthHandlers.Health)
            .WithName("OrdersHealth")
            .Produces(200)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy
    }
}

/// <summary>Operational endpoint handlers. They return <see cref="IResult" />: the body is minimal or detailed
/// depending on the environment (BuildInfoProvider), so there is no single typed shape.</summary>
internal static class OrdersHealthHandlers
{
    public static IResult Health(IHostEnvironment env, IConfiguration cfg)
    {
        var timestampUtc = DateTime.UtcNow.ToString("O");

        if (!BuildInfoProvider.ShouldExposeOperationalMetadata(env))
        {
            return Results.Json(new
            {
                module = "Orders",
                status = "Healthy",
                timestampUtc
            });
        }

        var response = new
        {
            module = "Orders",
            status = "Healthy",
            timestampUtc,
            environment = BuildInfoProvider.GetEnvironment(env),
            version = BuildInfoProvider.GetInformationalVersion(typeof(OrdersModule).Assembly),
            service = BuildInfoProvider.GetServiceName(cfg)
        };
        return Results.Json(response);
    }
}
