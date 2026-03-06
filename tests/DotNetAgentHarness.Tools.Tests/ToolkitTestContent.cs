namespace DotNetAgentHarness.Tools.Tests;

internal static class ToolkitTestContent
{
    public static void WritePromptToolkit(TestRepositoryBuilder repo)
    {
        WritePersonas(repo);
        WriteSkills(repo);
        WriteSubagents(repo);
        WriteCommands(repo);
        repo.WriteFile("src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
    }

    private static void WritePersonas(TestRepositoryBuilder repo)
    {
        repo.WriteFile(".rulesync/personas/architect.json", """
            {
              "id": "architect",
              "displayName": "Architecture Planner",
              "description": "Designs and evaluates .NET solution structure, framework choices, and technical tradeoffs.",
              "purpose": "Produce architecture recommendations grounded in detected repository context and explicit tradeoffs.",
              "defaultSubagent": "dotnet-architect",
              "preferredSubagents": ["dotnet-architect"],
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
              "description": "Reviews .NET changes for correctness, safety, performance, and maintainability.",
              "purpose": "Produce findings-first review output with evidence, impact, and concrete fixes.",
              "defaultSubagent": "dotnet-code-review-agent",
              "preferredSubagents": ["dotnet-code-review-agent"],
              "defaultSkills": ["dotnet-csharp-coding-standards", "dotnet-csharp-async-patterns", "dotnet-csharp-code-smells", "dotnet-agent-gotchas"],
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
              "displayName": "Implementation Driver",
              "description": "Implements and refactors .NET code using repository context, recommended skills, and validation loops.",
              "purpose": "Turn a user request into safe, target-specific code changes and verify them with repository-native checks.",
              "preferredSubagents": ["dotnet-architect", "dotnet-testing-specialist"],
              "defaultSkills": ["dotnet-advisor", "dotnet-version-detection", "dotnet-project-analysis", "dotnet-agent-gotchas"],
              "allowedTools": ["Read", "Grep", "Glob", "Bash", "Edit", "Write"],
              "outputContract": ["verify changes"],
              "intentSignals": ["implement", "fix", "change"],
              "requestDirectives": ["Rewrite the request into a target-specific implementation task."]
            }
            """);
        repo.WriteFile(".rulesync/personas/tester.json", """
            {
              "id": "tester",
              "displayName": "Verification Specialist",
              "description": "Designs and executes .NET validation strategy, test additions, and failure triage.",
              "purpose": "Drive verification-first execution using repository-native tests, fixtures, and validation commands.",
              "defaultSubagent": "dotnet-testing-specialist",
              "preferredSubagents": ["dotnet-testing-specialist"],
              "defaultSkills": ["dotnet-testing-strategy", "dotnet-xunit", "dotnet-integration-testing", "dotnet-agent-harness-test-framework"],
              "allowedTools": ["Read", "Grep", "Glob", "Bash", "Edit", "Write"],
              "outputContract": ["show the exact validation command used"],
              "intentSignals": ["test", "verify", "coverage"],
              "requestDirectives": ["Rewrite the request into a verification plan with explicit scope."]
            }
            """);
    }

    private static void WriteSkills(TestRepositoryBuilder repo)
    {
        repo.WriteFile(".rulesync/skills/dotnet-advisor/SKILL.md", Skill("dotnet-advisor", "Routes .NET work"));
        repo.WriteFile(".rulesync/skills/dotnet-version-detection/SKILL.md", Skill("dotnet-version-detection", "Detects SDK and TFM"));
        repo.WriteFile(".rulesync/skills/dotnet-project-analysis/SKILL.md", Skill("dotnet-project-analysis", "Analyzes project structure"));
        repo.WriteFile(".rulesync/skills/dotnet-agent-gotchas/SKILL.md", Skill("dotnet-agent-gotchas", "Finds .NET mistakes"));
        repo.WriteFile(".rulesync/skills/dotnet-solution-navigation/SKILL.md", Skill("dotnet-solution-navigation", "Navigates .NET solutions"));
        repo.WriteFile(".rulesync/skills/dotnet-csharp-coding-standards/SKILL.md", Skill("dotnet-csharp-coding-standards", "Coding standards"));
        repo.WriteFile(".rulesync/skills/dotnet-csharp-async-patterns/SKILL.md", Skill("dotnet-csharp-async-patterns", "Async guidance"));
        repo.WriteFile(".rulesync/skills/dotnet-csharp-code-smells/SKILL.md", Skill("dotnet-csharp-code-smells", "Code smells"));
        repo.WriteFile(".rulesync/skills/dotnet-testing-strategy/SKILL.md", Skill("dotnet-testing-strategy", "Testing strategy"));
        repo.WriteFile(".rulesync/skills/dotnet-xunit/SKILL.md", Skill("dotnet-xunit", "xUnit guidance"));
        repo.WriteFile(".rulesync/skills/dotnet-integration-testing/SKILL.md", Skill("dotnet-integration-testing", "Integration testing"));
        repo.WriteFile(".rulesync/skills/dotnet-agent-harness-test-framework/SKILL.md", Skill("dotnet-agent-harness-test-framework", "Harness test framework"));
    }

    private static void WriteSubagents(TestRepositoryBuilder repo)
    {
        repo.WriteFile(".rulesync/subagents/dotnet-architect.md", Agent("dotnet-architect", "Architecture specialist"));
        repo.WriteFile(".rulesync/subagents/dotnet-code-review-agent.md", Agent("dotnet-code-review-agent", "Code review specialist"));
        repo.WriteFile(".rulesync/subagents/dotnet-testing-specialist.md", Agent("dotnet-testing-specialist", "Testing specialist"));
    }

    private static void WriteCommands(TestRepositoryBuilder repo)
    {
        repo.WriteFile(".rulesync/commands/dotnet-agent-harness-bootstrap.md", Command("Bootstrap the local runtime and RuleSync targets"));
        repo.WriteFile(".rulesync/commands/dotnet-agent-harness-prepare-message.md", Command("Prepare a prompt bundle"));
        repo.WriteFile(".rulesync/commands/dotnet-agent-harness-search.md", Command("Search toolkit content"));
        repo.WriteFile(".rulesync/commands/dotnet-agent-harness-compare-prompts.md", Command("Compare prompt bundles"));
        repo.WriteFile(".rulesync/commands/dotnet-agent-harness-incident.md", Command("Record prompt incidents"));
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
