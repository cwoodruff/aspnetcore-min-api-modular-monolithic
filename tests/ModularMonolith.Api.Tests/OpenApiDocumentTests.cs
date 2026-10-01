using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ModularMonolith.Api.Tests;

/// <summary>One OpenAPI document per module, plus the combined v1 (phase 7). Swagger is served in Development.</summary>
public class OpenApiDocumentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static TheoryData<string, string> ModuleDocuments() => new()
    {
        { "catalog", "/api/catalog/" },
        { "orders", "/api/orders/" },
        { "admin", "/api/admin/" },
        { "identity", "/api/identity/" },
        { "reporting", "/api/reporting/" }
    };

    [Theory]
    [MemberData(nameof(ModuleDocuments))]
    public async Task EachModulesDocument_ContainsOnlyThatModulesPaths(string document, string prefix)
    {
        var paths = await PathsAsync(document);

        paths.Should().NotBeEmpty();
        paths.Should().AllSatisfy(path => path.Should().StartWith(prefix));
    }

    [Fact]
    public async Task TheCombinedDocument_ContainsEveryModulesPaths()
    {
        var all = await PathsAsync("v1");

        foreach (var (document, _) in HostCompositionDocuments())
        {
            all.Should().Contain(await PathsAsync(document));
        }
    }

    private static IEnumerable<(string Document, string Tag)> HostCompositionDocuments() =>
        HostComposition.ModuleOpenApiDocuments.Select(entry => (entry.Key, entry.Value));

    private async Task<List<string>> PathsAsync(string document)
    {
        var json = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/swagger/{document}/swagger.json");
        return json.GetProperty("paths").EnumerateObject().Select(path => path.Name).ToList();
    }
}
