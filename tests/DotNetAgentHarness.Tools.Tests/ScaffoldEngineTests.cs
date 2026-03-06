using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class ScaffoldEngineTests
{
    [Fact]
    public void Plan_BuildsExpectedConsoleScaffoldSteps()
    {
        using var repo = new TestRepositoryBuilder();
        repo.WriteFile(".rulesync/templates/console/template.json", """
            {
              "id": "console",
              "displayName": "Console App",
              "description": "Console template",
              "dotNetTemplate": "console"
            }
            """);

        var plan = ScaffoldEngine.Plan(repo.Root, "console", "/tmp/ConsoleSample", "ConsoleSample");

        Assert.Equal("console", plan.TemplateId);
        Assert.Contains(plan.Steps, step => step.Command.Contains("dotnet new console", System.StringComparison.Ordinal));
        Assert.Contains(plan.Steps, step => step.Command.Contains("dotnet new xunit", System.StringComparison.Ordinal));
    }
}
