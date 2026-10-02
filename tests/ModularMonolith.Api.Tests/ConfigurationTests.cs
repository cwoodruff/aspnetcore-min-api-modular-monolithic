using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularMonolith.Api;
using SharedKernel.Concurrency;
using SharedKernel.TrafficControl;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Every per-module limit the code reads has a value in appsettings.json, so the limits can be found and
///     tuned without reading code (ADR-0013). The modules and policies come from the app itself: a new module
///     cache, queue, gate or rate-limit policy fails here until its setting is added.
/// </summary>
public class ConfigurationTests
{
    private static readonly IConfiguration Settings = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .Build();

    private static readonly string[] RateLimitSettings = ["PermitLimit", "WindowSeconds", "QueueLimit"];

    [Fact]
    public void EveryRateLimitPolicy_HasItsLimitsInAppsettings()
    {
        var policies = typeof(RateLimitPolicyRegistry).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        policies.Should().HaveCount(6);
        Missing(policies.SelectMany(policy => RateLimitSettings.Select(setting => $"RateLimiting:Policies:{policy}:{setting}"))).Should().BeEmpty();
    }

    [Fact]
    public void EveryModuleCacheQueueAndGate_HasItsSizeInAppsettings()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        HostComposition.ConfigureServices(builder);
        string[] KeysOf<T>() => [.. builder.Services
            .Where(descriptor => descriptor.ServiceType == typeof(T) && descriptor.IsKeyedService)
            .Select(descriptor => (string)descriptor.ServiceKey!)
            .Distinct()];

        var caches = KeysOf<IMemoryCache>();
        var queues = KeysOf<ModuleWorkQueue>();
        var gates = KeysOf<ModuleGate>();
        caches.Should().BeEquivalentTo("Catalog", "Orders", "Administration");
        queues.Should().BeEquivalentTo("Orders", "Reporting");
        gates.Should().BeEquivalentTo("Reporting");

        Missing([
            .. caches.Select(module => $"Caching:Modules:{module}:SizeLimit"),
            .. queues.Select(module => $"Concurrency:{module}:QueueCapacity"),
            .. gates.Select(module => $"Concurrency:{module}:MaxConcurrentExpensive")
        ]).Should().BeEmpty();
    }

    private static List<string> Missing(IEnumerable<string> keys) => [.. keys.Where(key => Settings[key] is null)];
}
