using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class SkillTestEngineTests
{
    [Fact]
    public void Run_ExecutesStaticSkillAssertions()
    {
        using var repo = new TestRepositoryBuilder();
        repo.WriteFile(".rulesync/skills/example-skill/SKILL.md", """
            ---
            name: example-skill
            description: Example skill
            targets: ['*']
            ---
            # example-skill

            Private fields should use _camelCase naming.
            """);
        repo.WriteFile(".rulesync/skills/example-skill/test-cases/001-basic.yml", """
            name: Basic validation
            expected_output_contains:
              - "_camelCase"
            validation:
              - pattern: "_camelCase"
                description: "underscore naming present"
            """);

        var suite = SkillTestEngine.Run(repo.Root, "example-skill", failFast: false);

        Assert.True(suite.Passed);
        Assert.Single(suite.Skills);
        Assert.Equal(1, suite.SkillsWithCases);
        Assert.Equal(0, suite.SkillsWithoutCases);
        Assert.All(suite.Skills[0].Checks, check => Assert.True(check.Passed, check.Message));
    }

    [Fact]
    public void Run_ExecutesStructuredSkillCases()
    {
        using var repo = new TestRepositoryBuilder();
        repo.WriteFile(".rulesync/skills/example-skill/SKILL.md", """
            ---
            name: example-skill
            description: Example skill
            targets: ['*']
            ---
            # example-skill

            Private fields should use _camelCase naming.
            """);
        repo.WriteFile(".rulesync/skills/example-skill/test-cases/002-structured.yml", """
            name: Structured validation
            setup:
              - command: echo seeded > seed.txt
            tests:
              - name: Frontmatter contract
                expected:
                  frontmatter:
                    name: example-skill
                  output_contains:
                    - "_camelCase"
              - name: Command execution
                command: echo generated > output.txt
                expected:
                  status: success
                  file_exists:
                    - output.txt
                    - seed.txt
                  no_errors: true
            """);

        var suite = SkillTestEngine.Run(repo.Root, "example-skill", failFast: false, filter: "Structured");

        Assert.True(suite.Passed);
        Assert.Equal(1, suite.TotalCases);
        Assert.Equal(1, suite.SkillsWithCases);
        Assert.Equal(0, suite.SkillsWithoutCases);
        Assert.Contains(suite.Skills[0].Checks, check => check.Name.Contains("frontmatter.name", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(suite.Skills[0].Checks, check => check.Name.Contains("file_exists 'output.txt'", System.StringComparison.OrdinalIgnoreCase));
    }
}
