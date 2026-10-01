using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     The host under test, pointed at a fresh clone of the seeded Chinook database. Factories derived
///     with <c>WithWebHostBuilder</c> run this too, so every host gets its own database.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppDatabase"] = PostgresFixture.CreateSeededDatabase()
            });
        });
    }
}
