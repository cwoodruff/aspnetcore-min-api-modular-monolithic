using System.Text.Json;

namespace ModularMonolith.Module.Tests.Hosting;

internal static class Json
{
    /// <summary>The response body, after asserting the status.</summary>
    public static async Task<JsonElement> ReadAsync(this HttpResponseMessage response, System.Net.HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != expected)
        {
            throw new Xunit.Sdk.XunitException($"Expected {(int)expected}, got {(int)response.StatusCode}: {body}");
        }

        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    public static StringContent Body(object value) =>
        new(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json");
}
