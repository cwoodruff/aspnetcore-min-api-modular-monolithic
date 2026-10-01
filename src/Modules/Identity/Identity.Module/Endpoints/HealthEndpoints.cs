using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace Identity.Modules.Endpoints;

internal static class IdentityHealthEndpoints
{
    public static void MapIdentityHealthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/health", IdentityHealthHandlers.Health)
            .WithName("IdentityHealth")
            .Produces(200)
            .WithTags("Identity")
            .Produces(429); // Rate limited by the module group's policy
    }
}

/// <summary>Operational endpoint handlers. They return <see cref="IResult" />: the body is minimal or detailed
/// depending on the environment (BuildInfoProvider), so there is no single typed shape.</summary>
internal static class IdentityHealthHandlers
{
    public static IResult Health(IHostEnvironment env, IConfiguration cfg)
    {
        var timestampUtc = DateTime.UtcNow.ToString("O");

        if (!BuildInfoProvider.ShouldExposeOperationalMetadata(env))
        {
            return Results.Json(new
            {
                module = "Identity",
                status = "Healthy",
                timestampUtc
            });
        }

        var response = new
        {
            module = "Identity",
            status = "Healthy",
            timestampUtc,
            environment = BuildInfoProvider.GetEnvironment(env),
            version = BuildInfoProvider.GetInformationalVersion(typeof(IdentityModule).Assembly),
            service = BuildInfoProvider.GetServiceName(cfg)
        };
        return Results.Json(response);
    }
}
