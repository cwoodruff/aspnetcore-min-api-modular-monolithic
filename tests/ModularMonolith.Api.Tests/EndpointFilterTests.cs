using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using SharedKernel.Validation;

namespace ModularMonolith.Api.Tests;

/// <summary>Endpoint filter behaviour the modules rely on (phase 7).</summary>
public class EndpointFilterTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task GroupFilters_RunBeforeEndpointFilters_InTheOrderTheyWereAdded()
    {
        // The documented rule this repo relies on: a module group's filters (metrics) wrap every endpoint
        // filter (validation), and filters on one builder run in the order they were added.
        var calls = new List<string>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();

        var group = app.MapGroup("/g")
            .AddEndpointFilter(Marker(calls, "group 1"))
            .AddEndpointFilter(Marker(calls, "group 2"));
        group.MapGet("/x", () =>
            {
                calls.Add("handler");
                return "ok";
            })
            .AddEndpointFilter(Marker(calls, "endpoint 1"))
            .AddEndpointFilter(Marker(calls, "endpoint 2"));

        await app.StartAsync();
        (await app.GetTestClient().GetStringAsync("/g/x")).Should().Be("ok");

        calls.Should().Equal("group 1", "group 2", "endpoint 1", "endpoint 2", "handler");
    }

    [Fact]
    public async Task ValidationFilter_RejectsBeforeTheHandler_WithTheHostsValidationProblemShape()
    {
        var client = factory.WithAdminTenantUser(permissions: [Permissions.AdministrationRead, Permissions.AdministrationWrite])
            .CreateClient();
        client.UseBearer(await TestAuthHelpers.GetAccessTokenAsync(client));
        var genresBefore = (await client.GetFromJsonAsync<JsonElement>("/api/admin/genres/")).GetArrayLength();

        var response = await client.PostAsync("/api/admin/genres",
            new StringContent(JsonSerializer.Serialize(new { name = new string('x', 121) }), Encoding.UTF8, "application/json"));

        // One error shape for the filter and for the exception fallback (Program.WriteProblemDetailsResponseAsync).
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("title").GetString().Should().Be(ValidationFilter<object>.Title);
        problem.GetProperty("detail").GetString().Should().Be(ValidationFilter<object>.Detail);
        problem.GetProperty("type").GetString().Should().Be(ValidationFilter<object>.Type);
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
        problem.GetProperty("errors").EnumerateObject().Select(error => error.Name).Should().Equal("Name");

        // The handler never ran.
        (await client.GetFromJsonAsync<JsonElement>("/api/admin/genres/")).GetArrayLength().Should().Be(genresBefore);
    }

    private static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> Marker(
        List<string> calls, string name) =>
        async (context, next) =>
        {
            calls.Add(name);
            return await next(context);
        };
}
