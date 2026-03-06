using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNetAgentHarness.Tools.Engine;

public static class RecommendationEngine
{
    private static readonly RecommendationRule[] Rules =
    {
        new("foundation", CatalogKinds.Skill, "dotnet-advisor", 100, "Core routing baseline for .NET work"),
        new("foundation", CatalogKinds.Skill, "dotnet-version-detection", 95, "Version and SDK detection should always run first"),
        new("foundation", CatalogKinds.Skill, "dotnet-project-analysis", 95, "Project structure detection is required for routing"),
        new("foundation", CatalogKinds.Skill, "dotnet-agent-gotchas", 70, ".NET-specific failure modes should be loaded early"),
        new("foundation", CatalogKinds.Skill, "dotnet-solution-navigation", 70, "Entry-point and solution navigation guidance is broadly useful"),
        new("aspnetcore", CatalogKinds.Skill, "dotnet-minimal-apis", 88, "ASP.NET Core detected"),
        new("aspnetcore", CatalogKinds.Skill, "dotnet-api-security", 78, "API security patterns apply to ASP.NET Core repos"),
        new("aspnetcore", CatalogKinds.Skill, "dotnet-middleware-patterns", 74, "Middleware composition is relevant for web projects"),
        new("openapi", CatalogKinds.Skill, "dotnet-openapi", 82, "OpenAPI packages detected"),
        new("efcore", CatalogKinds.Skill, "dotnet-efcore-patterns", 88, "EF Core packages detected"),
        new("efcore", CatalogKinds.Skill, "dotnet-efcore-architecture", 82, "EF Core architecture guidance applies"),
        new("efcore", CatalogKinds.Skill, "dotnet-data-access-strategy", 68, "Data access tradeoffs matter when EF Core is present"),
        new("blazor", CatalogKinds.Skill, "dotnet-blazor-patterns", 88, "Blazor packages or SDK detected"),
        new("blazor", CatalogKinds.Skill, "dotnet-blazor-components", 80, "Blazor component design is relevant"),
        new("blazor", CatalogKinds.Skill, "dotnet-blazor-testing", 70, "Blazor test guidance applies"),
        new("maui", CatalogKinds.Skill, "dotnet-maui-development", 88, "MAUI project detected"),
        new("maui", CatalogKinds.Skill, "dotnet-maui-testing", 75, "MAUI app testing guidance applies"),
        new("maui", CatalogKinds.Skill, "dotnet-maui-aot", 65, "MAUI apps benefit from startup and size guidance"),
        new("aspire", CatalogKinds.Skill, "dotnet-aspire-patterns", 90, ".NET Aspire packages detected"),
        new("aspire", CatalogKinds.Skill, "dotnet-observability", 80, "Distributed apps should wire in observability"),
        new("resilience", CatalogKinds.Skill, "dotnet-resilience", 75, "Resilience libraries detected"),
        new("cli", CatalogKinds.Skill, "dotnet-system-commandline", 85, "Console application detected"),
        new("cli", CatalogKinds.Skill, "dotnet-cli-architecture", 80, "CLI structure guidance applies"),
        new("cli", CatalogKinds.Skill, "dotnet-cli-distribution", 68, "CLI packaging and distribution are relevant"),
        new("testing", CatalogKinds.Skill, "dotnet-testing-strategy", 86, "Test projects detected"),
        new("testing", CatalogKinds.Skill, "dotnet-xunit", 84, "xUnit detected"),
        new("testing", CatalogKinds.Skill, "dotnet-integration-testing", 72, "Integration testing guidance commonly applies"),
        new("containers", CatalogKinds.Skill, "dotnet-containers", 76, "Container files detected"),
        new("containers", CatalogKinds.Skill, "dotnet-container-deployment", 66, "Deployment guidance likely applies"),
        new("github-actions", CatalogKinds.Skill, "dotnet-gha-patterns", 74, "GitHub Actions workflow detected"),
        new("github-actions", CatalogKinds.Skill, "dotnet-gha-build-test", 68, "Build and test workflow guidance applies"),
        new("azure-devops", CatalogKinds.Skill, "dotnet-ado-patterns", 74, "Azure DevOps pipeline detected"),
        new("azure-devops", CatalogKinds.Skill, "dotnet-ado-build-test", 68, "ADO build and test guidance applies"),
        new("foundation", CatalogKinds.Subagent, "dotnet-architect", 92, "Primary architecture agent should be available early"),
        new("aspnetcore", CatalogKinds.Subagent, "dotnet-aspnetcore-specialist", 82, "Web stack specialist fits the repo"),
        new("blazor", CatalogKinds.Subagent, "dotnet-blazor-specialist", 82, "Blazor specialist fits the repo"),
        new("maui", CatalogKinds.Subagent, "dotnet-maui-specialist", 82, "MAUI specialist fits the repo"),
        new("testing", CatalogKinds.Subagent, "dotnet-testing-specialist", 78, "Testing specialist fits the repo"),
        new("containers", CatalogKinds.Subagent, "dotnet-cloud-specialist", 70, "Deployment and cloud constraints likely matter"),
        new("foundation", CatalogKinds.Subagent, "dotnet-code-review-agent", 68, "General .NET code review agent is broadly useful"),
        new("foundation", CatalogKinds.Command, "init-project", 90, "Repo bootstrap should happen before deeper work"),
        new("foundation", CatalogKinds.Command, "dotnet-agent-harness-search", 72, "Skill discovery is useful in unfamiliar repos"),
        new("foundation", CatalogKinds.Command, "dotnet-agent-harness-prepare-message", 68, "Prompt assembly is useful before implementation, review, or planning"),
        new("foundation", CatalogKinds.Command, "dotnet-agent-harness-profile", 60, "Local profiling helps identify expensive content"),
        new("foundation", CatalogKinds.Command, "dotnet-agent-harness-compare", 56, "Comparing candidate skills helps choose the right guidance"),
        new("foundation", CatalogKinds.Command, "dotnet-agent-harness-compare-prompts", 58, "Prompt diffs help review persona and tool-policy changes"),
        new("foundation", CatalogKinds.Command, "dotnet-agent-harness-test", 56, "Validation command helps maintain skill quality"),
        new("foundation", CatalogKinds.Command, "dotnet-agent-harness-incident", 52, "Incident capture preserves prompt evidence for regression tracking")
    };

    public static RecommendationBundle Recommend(RepositoryProfile profile, ToolkitCatalog catalog, int limitPerKind = 5)
    {
        var signals = BuildSignals(profile);

        var skills = RecommendByKind(catalog, CatalogKinds.Skill, signals, limitPerKind);
        var subagents = RecommendByKind(catalog, CatalogKinds.Subagent, signals, limitPerKind);
        var commands = RecommendByKind(catalog, CatalogKinds.Command, signals, limitPerKind);

        return new RecommendationBundle
        {
            Profile = profile,
            Skills = skills,
            Subagents = subagents,
            Commands = commands
        };
    }

    private static List<RecommendationItem> RecommendByKind(ToolkitCatalog catalog, string kind, HashSet<string> signals, int limit)
    {
        var candidates = new Dictionary<string, MutableRecommendation>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in Rules.Where(rule => rule.Kind == kind))
        {
            if (!signals.Contains(rule.Signal))
            {
                continue;
            }

            var item = catalog.Find(rule.Id);
            if (item is null || item.Kind != kind)
            {
                continue;
            }

            if (!candidates.TryGetValue(rule.Id, out var current))
            {
                current = new MutableRecommendation(item);
                candidates[rule.Id] = current;
            }

            current.Score += rule.Score;
            current.Reasons.Add(rule.Reason);
        }

        return candidates.Values
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Item.Id, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(candidate => new RecommendationItem
            {
                Id = candidate.Item.Id,
                Name = candidate.Item.Name,
                Kind = candidate.Item.Kind,
                Score = candidate.Score,
                Reasons = candidate.Reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                FilePath = candidate.Item.FilePath
            })
            .ToList();
    }

    private static HashSet<string> BuildSignals(RepositoryProfile profile)
    {
        var signals = new HashSet<string>(profile.Technologies, StringComparer.OrdinalIgnoreCase)
        {
            "foundation"
        };

        if (profile.DominantProjectKind == "web")
        {
            signals.Add("aspnetcore");
        }

        if (profile.DominantProjectKind == "console")
        {
            signals.Add("cli");
        }

        if (profile.DominantProjectKind == "blazor")
        {
            signals.Add("blazor");
        }

        if (profile.DominantProjectKind == "maui")
        {
            signals.Add("maui");
        }

        if (profile.TestProjectCount > 0)
        {
            signals.Add("testing");
        }

        if (profile.DominantTestFramework == "xunit")
        {
            signals.Add("testing");
        }

        return signals;
    }

    private sealed class MutableRecommendation
    {
        public MutableRecommendation(CatalogItem item)
        {
            Item = item;
        }

        public CatalogItem Item { get; }
        public int Score { get; set; }
        public List<string> Reasons { get; } = new();
    }

    private sealed record RecommendationRule(string Signal, string Kind, string Id, int Score, string Reason);
}
