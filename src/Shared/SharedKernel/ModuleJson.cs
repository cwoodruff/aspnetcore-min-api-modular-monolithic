using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SharedKernel;

/// <summary>
/// The JSON options SharedKernel serializes with (outbox payloads, the shared L2 cache), resolved keyed by
/// <see cref="OptionsKey" />. SharedKernel only asks the options for type information; whoever composes the
/// app decides where it comes from. That keeps SharedKernel free of reflection-based serialization
/// (Native AOT ready); an AOT host registers options whose resolver chains the modules' source-generated
/// <c>JsonSerializerContext</c>s instead of calling <see cref="AddReflectionJsonSerialization" />.
/// </summary>
public static class ModuleJson
{
    public const string OptionsKey = "ModularMonolith.Json";

    /// <summary>
    /// Registers web-default JSON options with the reflection-based resolver, unless options are already
    /// registered. The host and tests call this; it is not trimming- or AOT-safe.
    /// </summary>
    [RequiresUnreferencedCode("Uses the reflection-based JSON type resolver.")]
    [RequiresDynamicCode("Uses the reflection-based JSON type resolver.")]
    public static IServiceCollection AddReflectionJsonSerialization(this IServiceCollection services)
    {
        services.TryAddKeyedSingleton(OptionsKey, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        });
        return services;
    }

    internal static JsonTypeInfo<T> TypeInfo<T>(this JsonSerializerOptions options) =>
        (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
}
