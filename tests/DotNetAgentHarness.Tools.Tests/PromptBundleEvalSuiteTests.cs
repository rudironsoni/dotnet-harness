using System.IO;
using System.Linq;
using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class PromptBundleEvalSuiteTests
{
    [Fact]
    public void Run_ReturnsPassingPromptChecks()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var checks = PromptBundleEvalSuite.Run(repo.Root);

        Assert.Contains(checks, check => check.Name == "prompt-persona-precedence" && check.Passed);
        Assert.Contains(checks, check => check.Name == "prompt-tool-policy" && check.Passed);
        Assert.Contains(checks, check => check.Name == "prompt-platform-rendering" && check.Passed);
    }

    [Fact]
    public void Validate_SkillMode_IncludesPromptBundleChecks()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var report = ValidationEngine.Validate(repo.Root, "skill");

        Assert.Contains(report.Checks, check => check.Name == "prompt-persona-precedence");
        Assert.Contains(report.Checks, check => check.Name == "prompt-tool-policy");
        Assert.Contains(report.Checks, check => check.Name == "prompt-platform-rendering");
        Assert.True(report.Checks.Where(check => check.Name.StartsWith("prompt-", System.StringComparison.Ordinal)).All(check => check.Passed));
    }

    [Fact]
    public void WritePreparedMessageEvidence_PersistsReportAndPrompt()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var report = PromptBundleBuilder.Prepare(repo.Root, "Review the repo for validation risks", new PromptAssemblyOptions
        {
            PersonaId = "reviewer",
            TargetPath = "src/App/App.csproj",
            Platform = PromptPlatforms.ClaudeCode
        });

        var evidence = RepoStateStore.WritePreparedMessageEvidence(repo.Root, report, "reviewer-check");

        Assert.True(File.Exists(evidence.ReportPath));
        Assert.True(File.Exists(evidence.PromptPath));
        Assert.Contains("reviewer-check", evidence.ReportPath);
        Assert.Contains("Platform: claudecode", File.ReadAllText(evidence.PromptPath));
        Assert.Contains("\"EvidenceId\": \"reviewer-check\"", File.ReadAllText(evidence.ReportPath));
    }
}
