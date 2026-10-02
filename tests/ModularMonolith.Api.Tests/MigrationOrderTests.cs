using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularMonolith.Api;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     The host migrates Administration, Catalog, Orders, then Reporting, whose views read the other three
///     schemas (ADR-0003, ADR-0014). Checked on the contexts the app registers; no database needed.
/// </summary>
public class MigrationOrderTests
{
    [Fact]
    public void TheHostsContexts_MigrateWithReportingLast()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        HostComposition.ConfigureServices(builder);
        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var ordered = DbSeeder.InMigrationOrder(scope.ServiceProvider.GetKeyedServices<DbContext>(KeyedService.AnyKey));

        ordered.Select(context => context.Model.GetDefaultSchema())
            .Should().Equal("administration", "catalog", "orders", "reporting");
    }

    [Fact]
    public void AContextWithNoPlaceInTheOrder_IsRefused()
    {
        using var stray = new StrayContext();

        FluentActions.Invoking(() => DbSeeder.InMigrationOrder([stray]))
            .Should().Throw<InvalidOperationException>().WithMessage("*StrayContext*");
    }

    private sealed class StrayContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseNpgsql("Host=unused;Database=unused");

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema("stray");
    }
}
