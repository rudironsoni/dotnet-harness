using System;
using System.Linq;
using System.Reflection;

namespace DotNetAgentHarness.Tools.Engine;

public static class ToolkitRuntimeMetadata
{
    public const string PackageId = "Rudironsoni.DotNetAgentHarness";
    public const string ToolCommandName = "dotnet-agent-harness";
    public const string RuleSyncSourceRepository = "rudironsoni/dotnet-agent-harness";
    public const string RuleSyncSourcePath = ".rulesync";

    public static string ResolveToolVersion(string? requestedVersion = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
        {
            return requestedVersion.Trim();
        }

        var assembly = typeof(ToolkitRuntimeMetadata).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var normalizedInformational = informational?.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(normalizedInformational))
        {
            return normalizedInformational;
        }

        var version = assembly.GetName().Version;
        if (version is not null)
        {
            return $"{version.Major}.{Math.Max(version.Minor, 0)}.{Math.Max(version.Build, 0)}";
        }

        return "1.0.0";
    }
}

