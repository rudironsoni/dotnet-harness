using DotNetAgentHarness.Evals.Engine;
using Xunit;

namespace DotNetAgentHarness.Evals.Tests;

public class TriggerEvaluatorTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("no_trigger")]
    [InlineData("null")]
    [InlineData("no-skill")]
    [InlineData("")]
    [InlineData(" ")]
    public void Display_NormalizesNoneAliases(string value)
    {
        var display = TriggerEvaluator.Display(value);

        Assert.Equal("none", display);
    }

    [Fact]
    public void Evaluate_Passes_WhenExpectedAndActualAreBothNoneAliases()
    {
        var result = TriggerEvaluator.Evaluate("none", "no_trigger");

        Assert.True(result.Passed);
    }

    [Fact]
    public void Evaluate_Fails_WhenExpectedAndActualDiffer()
    {
        var result = TriggerEvaluator.Evaluate("dotnet-architect", "dotnet-github-docs");

        Assert.False(result.Passed);
        Assert.Contains("Expected trigger 'dotnet-architect'", result.Message);
    }
}
