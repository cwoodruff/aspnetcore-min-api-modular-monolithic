using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using ModularMonolith.Api;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Demo is Development's twin for in-memory logins and Swagger, so its accounts come from user secrets the
///     same way (README, "Development-only in-memory users"), and it has the compose database's connection strings.
/// </summary>
public class DemoEnvironmentTests
{
    [Theory]
    [InlineData("Development", 1)]
    [InlineData(HostComposition.DemoEnvironment, 1)]
    [InlineData("Production", 0)]
    public void UserSecrets_LoadOnce_InDevelopmentAndDemo_BeforeEnvironmentVariables(string environment, int expected)
    {
        var sources = Configure(environment).Configuration.Sources.ToList();

        var secrets = sources.FindAll(IsUserSecrets);
        secrets.Should().HaveCount(expected);
        if (expected == 1)
        {
            // Same precedence as CreateBuilder gives Development: after appsettings.{environment}.json, and before
            // the unprefixed environment variables, so an environment variable still overrides a secret.
            var index = sources.IndexOf(secrets[0]);
            index.Should().BeGreaterThan(sources.FindIndex(source =>
                source is JsonConfigurationSource { Path: var path } && path == $"appsettings.{environment}.json"));
            index.Should().BeLessThan(sources.FindLastIndex(source =>
                source is EnvironmentVariablesConfigurationSource { Prefix: null or "" }));
        }
    }

    [Fact]
    public void Demo_HasTheComposeDatabaseAndMigratesOnStartup()
    {
        var configuration = Configure(HostComposition.DemoEnvironment).Configuration;

        configuration.GetConnectionString("AppDatabase").Should().Contain("Database=chinook");
        configuration.GetConnectionString("Reporting").Should().Contain("Username=reporting");
        configuration.GetValue<bool>("Database:MigrateAndSeedOnStartup").Should().BeTrue();
    }

    private static WebApplicationBuilder Configure(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment,
            ApplicationName = typeof(HostComposition).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });
        HostComposition.ConfigureServices(builder);
        return builder;
    }

    private static bool IsUserSecrets(IConfigurationSource source) =>
        source is JsonConfigurationSource json && json.Path == "secrets.json";
}
