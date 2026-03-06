using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class RecommendationEngineTests
{
    [Fact]
    public void Recommend_RanksExpectedSkillsSubagentsAndCommands()
    {
        using var repo = new TestRepositoryBuilder();
        repo.WriteFile(".rulesync/skills/dotnet-advisor/SKILL.md", Skill("dotnet-advisor", "Routes .NET work"));
        repo.WriteFile(".rulesync/skills/dotnet-version-detection/SKILL.md", Skill("dotnet-version-detection", "Detects SDK and TFM"));
        repo.WriteFile(".rulesync/skills/dotnet-project-analysis/SKILL.md", Skill("dotnet-project-analysis", "Analyzes project structure"));
        repo.WriteFile(".rulesync/skills/dotnet-agent-gotchas/SKILL.md", Skill("dotnet-agent-gotchas", "Finds .NET mistakes"));
        repo.WriteFile(".rulesync/skills/dotnet-solution-navigation/SKILL.md", Skill("dotnet-solution-navigation", "Navigates .NET solutions"));
        repo.WriteFile(".rulesync/skills/dotnet-minimal-apis/SKILL.md", Skill("dotnet-minimal-apis", "Minimal API guidance"));
        repo.WriteFile(".rulesync/skills/dotnet-api-security/SKILL.md", Skill("dotnet-api-security", "API security"));
        repo.WriteFile(".rulesync/skills/dotnet-middleware-patterns/SKILL.md", Skill("dotnet-middleware-patterns", "Middleware patterns"));
        repo.WriteFile(".rulesync/skills/dotnet-openapi/SKILL.md", Skill("dotnet-openapi", "OpenAPI guidance"));
        repo.WriteFile(".rulesync/skills/dotnet-efcore-patterns/SKILL.md", Skill("dotnet-efcore-patterns", "EF Core patterns"));
        repo.WriteFile(".rulesync/skills/dotnet-efcore-architecture/SKILL.md", Skill("dotnet-efcore-architecture", "EF Core architecture"));
        repo.WriteFile(".rulesync/skills/dotnet-data-access-strategy/SKILL.md", Skill("dotnet-data-access-strategy", "Data access tradeoffs"));
        repo.WriteFile(".rulesync/subagents/dotnet-architect.md", Agent("dotnet-architect", "Architecture agent"));
        repo.WriteFile(".rulesync/subagents/dotnet-aspnetcore-specialist.md", Agent("dotnet-aspnetcore-specialist", "ASP.NET specialist"));
        repo.WriteFile(".rulesync/commands/init-project.md", Command("Initialize repository"));
        repo.WriteFile(".rulesync/commands/dotnet-agent-harness-search.md", Command("Search skills"));

        var catalog = ToolkitCatalogLoader.Load(repo.Root);
        var profile = new RepositoryProfile
        {
            RepoRoot = repo.Root,
            DominantProjectKind = "web",
            TestProjectCount = 1,
            DominantTestFramework = "xunit",
            Technologies = ["aspnetcore", "efcore", "openapi", "testing"]
        };

        var bundle = RecommendationEngine.Recommend(profile, catalog, 5);

        Assert.Contains(bundle.Skills, item => item.Id == "dotnet-minimal-apis");
        Assert.Contains(bundle.Skills, item => item.Id == "dotnet-efcore-patterns");
        Assert.Contains(bundle.Subagents, item => item.Id == "dotnet-architect");
        Assert.Contains(bundle.Commands, item => item.Id == "init-project");
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
            tags: ['dotnet']
            ---
            # {{name}}
            """;
    }

    private static string Command(string description)
    {
        return $$"""
            ---
            description: {{description}}
            targets: ['*']
            ---
            # /command
            """;
    }
}
