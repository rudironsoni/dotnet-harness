using System.IO;
using System.Linq;
using System.Text.Json;
using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class BootstrapEngineTests
{
    [Fact]
    public void Bootstrap_CreatesToolManifestRuleSyncConfigAndState()
    {
        using var repo = new TestRepositoryBuilder();
        repo.WriteFile("src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var report = BootstrapEngine.Bootstrap(repo.Root, new BootstrapOptions
        {
            Targets = [PromptPlatforms.ClaudeCode, PromptPlatforms.OpenCode, PromptPlatforms.CodexCli, PromptPlatforms.GeminiCli, PromptPlatforms.Copilot, PromptPlatforms.Antigravity]
        });

        Assert.True(report.Passed);
        Assert.True(File.Exists(Path.Combine(repo.Root, ".config", "dotnet-tools.json")));
        Assert.True(File.Exists(Path.Combine(repo.Root, "rulesync.jsonc")));
        Assert.True(File.Exists(Path.Combine(repo.Root, ".dotnet-agent-harness", "project-profile.json")));
        Assert.True(File.Exists(Path.Combine(repo.Root, ".dotnet-agent-harness", "recommendations.json")));
        Assert.True(File.Exists(Path.Combine(repo.Root, ".dotnet-agent-harness", "doctor-report.json")));
        Assert.True(File.Exists(Path.Combine(repo.Root, ".dotnet-agent-harness", "bootstrap-report.json")));
        Assert.Contains(report.Targets, target => target.Id == PromptPlatforms.GeminiCli);
        Assert.Contains(report.Targets, target => target.Id == PromptPlatforms.Antigravity);
        Assert.Contains(report.Warnings, warning => warning.Contains(".rulesync/", System.StringComparison.Ordinal));

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.Root, ".config", "dotnet-tools.json")));
        Assert.True(manifest.RootElement.TryGetProperty("tools", out var tools));
        Assert.True(tools.TryGetProperty(ToolkitRuntimeMetadata.PackageId.ToLowerInvariant(), out var toolEntry));
        Assert.Equal(ToolkitRuntimeMetadata.ToolCommandName, toolEntry.GetProperty("commands")[0].GetString());

        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.Root, "rulesync.jsonc")));
        Assert.Equal(ToolkitRuntimeMetadata.RuleSyncSourceRepository, config.RootElement.GetProperty("sources")[0].GetProperty("source").GetString());
        Assert.Contains(PromptPlatforms.Antigravity, config.RootElement.GetProperty("targets").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void Bootstrap_RejectsUnsupportedTargets()
    {
        using var repo = new TestRepositoryBuilder();

        var exception = Assert.Throws<System.ArgumentException>(() => BootstrapEngine.Bootstrap(repo.Root, new BootstrapOptions
        {
            Targets = ["cursor"]
        }));

        Assert.Contains("Unsupported prompt platform", exception.Message);
    }
}
