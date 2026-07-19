using System.Text;
using System.Text.Json;
using FluentAssertions;
using Identity.Modules.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ModularMonolith.Api.Tests;

public class HealthEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] HealthyStatuses = ["Healthy"];
    private static readonly string[] DataHealthyStatuses = ["Data-Healthy", "Degraded"];
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Theory]
    [InlineData("/", "root")]
    [InlineData("/api/music/health", "Music")]
    [InlineData("/api/orders/health", "Orders")]
    [InlineData("/api/admin/health", "Administration")]
    [InlineData("/api/reporting/health", "Reporting")]
    [InlineData("/api/identity/health", "Identity")]
    public async Task HealthEndpointsShouldReturnMinimalMetadataOutsideDevelopmentAndDemo(string url,
        string expectedModule)
    {
        var client = CreateClient("Production");

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await AssertResponseShapeAsync(response, expectedModule, HealthyStatuses, expectOperationalMetadata: false,
            expectDatabase: false);
    }

    [Theory]
    [InlineData("/", "root")]
    [InlineData("/api/music/health", "Music")]
    [InlineData("/api/orders/health", "Orders")]
    [InlineData("/api/admin/health", "Administration")]
    [InlineData("/api/reporting/health", "Reporting")]
    [InlineData("/api/identity/health", "Identity")]
    public async Task HealthEndpointsShouldReturnDetailedMetadataInDemo(string url, string expectedModule)
    {
        var client = CreateClient("Demo");

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await AssertResponseShapeAsync(response, expectedModule, HealthyStatuses, expectOperationalMetadata: true,
            expectDatabase: false);
    }

    [Theory]
    [InlineData("/api/music/data-health", "Music")]
    [InlineData("/api/orders/data-health", "Orders")]
    [InlineData("/api/admin/data-health", "Administration")]
    [InlineData("/api/reporting/data-health", "Reporting")]
    [InlineData("/api/identity/data-health", "Identity")]
    public async Task DataHealthEndpointsShouldReturnMinimalMetadataOutsideDevelopmentAndDemo(string url,
        string expectedModule)
    {
        var client = CreateClient("Production");

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await AssertResponseShapeAsync(response, expectedModule, DataHealthyStatuses,
            expectOperationalMetadata: false, expectDatabase: false);
    }

    [Theory]
    [InlineData("/api/music/data-health", "Music")]
    [InlineData("/api/orders/data-health", "Orders")]
    [InlineData("/api/admin/data-health", "Administration")]
    [InlineData("/api/reporting/data-health", "Reporting")]
    [InlineData("/api/identity/data-health", "Identity")]
    public async Task DataHealthEndpointsShouldReturnDetailedMetadataInDemo(string url, string expectedModule)
    {
        var client = CreateClient("Demo");

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await AssertResponseShapeAsync(response, expectedModule, DataHealthyStatuses, expectOperationalMetadata: true,
            expectDatabase: true);
    }

    [Fact]
    public async Task SwaggerShouldOnlyBeAvailableInDevelopmentAndDemo()
    {
        var productionClient = CreateClient("Production");
        var demoClient = CreateClient("Demo");

        var productionUiResponse = await productionClient.GetAsync("/swagger/index.html");
        var productionJsonResponse = await productionClient.GetAsync("/swagger/v1/swagger.json");
        var demoUiResponse = await demoClient.GetAsync("/swagger/index.html");
        var demoJsonResponse = await demoClient.GetAsync("/swagger/v1/swagger.json");

        productionUiResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        productionJsonResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        demoUiResponse.EnsureSuccessStatusCode();
        demoJsonResponse.EnsureSuccessStatusCode();
    }

    private HttpClient CreateClient(string environment)
    {
        return _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);

                if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(environment, "Demo", StringComparison.OrdinalIgnoreCase))
                {
                    builder.ConfigureServices(services =>
                    {
                        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IKeyMaterialService));
                        if (descriptor is not null)
                        {
                            services.Remove(descriptor);
                        }

                        services.AddSingleton<IKeyMaterialService, TestKeyMaterialService>();
                    });
                }
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    private static async Task AssertResponseShapeAsync(HttpResponseMessage response, string expectedModule,
        string[] validStatuses, bool expectOperationalMetadata, bool expectDatabase)
    {
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;

        root.TryGetProperty("module", out var moduleProp).Should().BeTrue();
        root.TryGetProperty("status", out var statusProp).Should().BeTrue();
        root.TryGetProperty("timestampUtc", out var timestampProp).Should().BeTrue();

        moduleProp.GetString().Should().Be(expectedModule);
        validStatuses.Should().Contain(statusProp.GetString());
        timestampProp.GetString().Should().NotBeNullOrWhiteSpace();

        root.TryGetProperty("environment", out var environmentProp).Should().Be(expectOperationalMetadata);
        root.TryGetProperty("version", out var versionProp).Should().Be(expectOperationalMetadata);
        root.TryGetProperty("service", out var serviceProp).Should().Be(expectOperationalMetadata);
        root.TryGetProperty("database", out var databaseProp).Should().Be(expectDatabase);

        if (expectOperationalMetadata)
        {
            environmentProp.GetString().Should().NotBeNullOrWhiteSpace();
            versionProp.GetString().Should().NotBeNullOrWhiteSpace();
            serviceProp.GetString().Should().NotBeNullOrWhiteSpace();
        }

        if (expectDatabase)
        {
            databaseProp.TryGetProperty("connected", out var connectedProp).Should().BeTrue();
            (connectedProp.ValueKind is JsonValueKind.True or JsonValueKind.False).Should().BeTrue();
        }
    }

    private sealed class TestKeyMaterialService : IKeyMaterialService
    {
        private static readonly SymmetricSecurityKey SigningKey =
            new(Encoding.UTF8.GetBytes("0123456789ABCDEF0123456789ABCDEF"));

        public SigningCredentials GetCurrentSigningCredentials()
        {
            return new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);
        }

        public IEnumerable<SecurityKey> GetValidationKeys()
        {
            yield return SigningKey;
        }

        public string GetCurrentKeyId()
        {
            return "test-key";
        }

        public object GetJwksDocument()
        {
            return new { keys = Array.Empty<object>() };
        }
    }
}
