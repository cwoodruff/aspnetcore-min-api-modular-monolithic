using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SharedKernel;

public static class BuildInfoProvider
{
    public static string GetInformationalVersion(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return infoVersion ?? assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    public static string GetEnvironment(IHostEnvironment env)
        => env.EnvironmentName;

    public static string GetServiceName(IConfiguration config)
        => config["ServiceName"] ?? "ModularMonolith.Api";
}
