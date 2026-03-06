using System.Linq;
using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class PromptBundleBuilderTests
{
    [Fact]
    public void Prepare_ResolvesReviewerPersonaAndConcreteTarget()
    {
        using var repo = new TestRepositoryBuilder();
        WritePersonas(repo);
        WriteFoundationSkills(repo);
        repo.WriteFile(".rulesync/skills/dotnet-csharp-coding-standards/SKILL.md", Skill("dotnet-csharp-coding-standards", "Coding standards"));
        repo.WriteFile(".rulesync/skills/dotnet-csharp-async-patterns/SKILL.md", Skill("dotnet-csharp-async-patterns", "Async guidance"));
        repo.WriteFile(".rulesync/skills/dotnet-csharp-code-smells/SKILL.md", Skill("dotnet-csharp-code-smells", "Code smells"));
        repo.WriteFile(".rulesync/subagents/dotnet-code-review-agent.md", Agent("dotnet-code-review-agent", "Code review specialist"));
        repo.WriteFile("src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var report = PromptBundleBuilder.Prepare(repo.Root, "Review this API service for async bugs and regressions", new PromptAssemblyOptions());

        Assert.Equal("reviewer", report.Persona.Id);
        Assert.Equal("src/App/App.csproj", report.Target.DisplayPath);
        Assert.NotNull(report.Subagent);
        Assert.Equal("dotnet-code-review-agent", report.Subagent!.Id);
        Assert.Contains(report.Skills, skill => skill.Id == "dotnet-csharp-code-smells");
        Assert.Contains("Code Reviewer", report.Bundle.SystemLayer);
        Assert.Contains("Forbidden tools: Edit, Write", report.Bundle.ToolLayer);
        Assert.Contains("persona: reviewer", report.EnhancedRequest);
        Assert.Contains("Request directives:", report.EnhancedRequest);
        Assert.Contains("Frame the task as evidence-driven review", report.EnhancedRequest);
        Assert.Equal(PromptPlatforms.Generic, report.RenderedPrompt.Platform);
        Assert.Equal(2, report.RenderedPrompt.Messages.Count);
        Assert.Contains("SYSTEM MESSAGE", report.RenderedPrompt.CompositeText);
    }

    [Fact]
    public void Prepare_RespectsExplicitPersonaAndFlagsAmbiguousTarget()
    {
        using var repo = new TestRepositoryBuilder();
        WritePersonas(repo);
        WriteFoundationSkills(repo);
        repo.WriteFile(".rulesync/subagents/dotnet-architect.md", Agent("dotnet-architect", "Architecture specialist"));
        repo.WriteFile("src/Api/Api.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        repo.WriteFile("src/Worker/Worker.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Worker">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var report = PromptBundleBuilder.Prepare(repo.Root, "Design the repository architecture", new PromptAssemblyOptions
        {
            PersonaId = "architect",
            Platform = "codex"
        });

        Assert.Equal("architect", report.Persona.Id);
        Assert.True(string.IsNullOrWhiteSpace(report.Target.TargetPath));
        Assert.True(report.Target.IsAmbiguous);
        Assert.Contains(report.Risks, risk => risk.Contains("target-resolution", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains("dotnet-advisor", report.Bundle.SkillLayer);
        Assert.Equal(PromptPlatforms.CodexCli, report.RenderedPrompt.Platform);
        Assert.Contains("SYSTEM", report.RenderedPrompt.Messages[0].Content);
        Assert.Contains("Assess the repository and recommend the best-fit architecture approach", report.EnhancedRequest);
    }

    [Fact]
    public void Prepare_ThrowsForUnknownPlatform()
    {
        using var repo = new TestRepositoryBuilder();
        WritePersonas(repo);
        WriteFoundationSkills(repo);

        var exception = Assert.Throws<System.ArgumentException>(() => PromptBundleBuilder.Prepare(repo.Root, "Implement a fix", new PromptAssemblyOptions
        {
            Platform = "unknown-platform"
        }));

        Assert.Contains("Unsupported prompt platform", exception.Message);
    }

    [Fact]
    public void Prepare_RendersGeminiAndAntigravityPlatforms()
    {
        using var repo = new TestRepositoryBuilder();
        WritePersonas(repo);
        WriteFoundationSkills(repo);
        repo.WriteFile("src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var gemini = PromptBundleBuilder.Prepare(repo.Root, "Review this API service for async bugs", new PromptAssemblyOptions
        {
            PersonaId = "reviewer",
            TargetPath = "src/App/App.csproj",
            Platform = "gemini"
        });
        var antigravity = PromptBundleBuilder.Prepare(repo.Root, "Implement a safer API change", new PromptAssemblyOptions
        {
            PersonaId = "implementer",
            TargetPath = "src/App/App.csproj",
            Platform = PromptPlatforms.Antigravity
        });

        Assert.Equal(PromptPlatforms.GeminiCli, gemini.RenderedPrompt.Platform);
        Assert.Contains("SYSTEM INSTRUCTIONS", gemini.RenderedPrompt.CompositeText);
        Assert.Contains("USER REQUEST", gemini.RenderedPrompt.CompositeText);

        Assert.Equal(PromptPlatforms.Antigravity, antigravity.RenderedPrompt.Platform);
        Assert.Contains("MISSION", antigravity.RenderedPrompt.CompositeText);
        Assert.Contains("WORKFLOW", antigravity.RenderedPrompt.CompositeText);
    }

    private static void WritePersonas(TestRepositoryBuilder repo)
    {
        repo.WriteFile(".rulesync/personas/architect.json", """
            {
              "id": "architect",
              "displayName": "Architect",
              "purpose": "Drive architecture planning.",
              "defaultSubagent": "dotnet-architect",
              "defaultSkills": ["dotnet-advisor", "dotnet-version-detection", "dotnet-project-analysis"],
              "allowedTools": ["Read", "Grep", "Glob", "Bash"],
              "forbiddenTools": ["Edit", "Write"],
              "outputContract": ["state tradeoffs"],
              "intentSignals": ["architecture", "design"],
              "requestDirectives": ["Summarize the project shape before recommending changes."]
            }
            """);
        repo.WriteFile(".rulesync/personas/reviewer.json", """
            {
              "id": "reviewer",
              "displayName": "Code Reviewer",
              "purpose": "Review code and surface findings.",
              "defaultSubagent": "dotnet-code-review-agent",
              "defaultSkills": ["dotnet-csharp-coding-standards", "dotnet-csharp-async-patterns", "dotnet-csharp-code-smells"],
              "allowedTools": ["Read", "Grep", "Glob", "Bash"],
              "forbiddenTools": ["Edit", "Write"],
              "outputContract": ["list findings first"],
              "intentSignals": ["review", "regression", "bug"],
              "requestDirectives": ["Frame the task as evidence-driven review, not implementation."]
            }
            """);
        repo.WriteFile(".rulesync/personas/implementer.json", """
            {
              "id": "implementer",
              "displayName": "Implementer",
              "purpose": "Implement changes safely.",
              "defaultSkills": ["dotnet-advisor", "dotnet-version-detection", "dotnet-project-analysis"],
              "allowedTools": ["Read", "Grep", "Glob", "Bash", "Edit", "Write"],
              "outputContract": ["verify changes"],
              "intentSignals": ["implement", "fix", "change"],
              "requestDirectives": ["Rewrite the request into a target-specific implementation task."]
            }
            """);
    }

    private static void WriteFoundationSkills(TestRepositoryBuilder repo)
    {
        repo.WriteFile(".rulesync/skills/dotnet-advisor/SKILL.md", Skill("dotnet-advisor", "Routes .NET work"));
        repo.WriteFile(".rulesync/skills/dotnet-version-detection/SKILL.md", Skill("dotnet-version-detection", "Detects SDK and TFM"));
        repo.WriteFile(".rulesync/skills/dotnet-project-analysis/SKILL.md", Skill("dotnet-project-analysis", "Analyzes project structure"));
        repo.WriteFile(".rulesync/skills/dotnet-agent-gotchas/SKILL.md", Skill("dotnet-agent-gotchas", "Finds .NET mistakes"));
    }

    private static string Skill(string name, string description)
    {
        return $$"""
            ---
            name: {{name}}
            description: {{description}}
            targets: ['*']
            tags: ['dotnet']
            ---
            # {{name}}
            """;
    }

    private static string Agent(string name, string description)
    {
        return $$"""
            ---
            name: {{name}}
            description: {{description}}
            targets: ['*']
            tags: ['dotnet', 'subagent']
            ---
            # {{name}}
            """;
    }
}
