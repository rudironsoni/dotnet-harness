using DotNetAgentHarness.Evals.Engine;
using DotNetAgentHarness.Evals.Models;
using Xunit;

namespace DotNetAgentHarness.Evals.Tests;

public class AssertionRunnerTests
{
    [Fact]
    public void Evaluate_ContainsAssertion_IsCaseInsensitive()
    {
        var assertion = new Assertion { Type = "contains", Value = "DOTNET" };

        var result = AssertionRunner.Evaluate("Use dotnet-agent-harness for this.", assertion);

        Assert.True(result.Passed);
    }

    [Fact]
    public void Evaluate_NotContainsAssertion_FailsWhenForbiddenFragmentPresent()
    {
        var assertion = new Assertion { Type = "not_contains", Value = "deprecated" };

        var result = AssertionRunner.Evaluate("This is deprecated behavior.", assertion);

        Assert.False(result.Passed);
        Assert.Contains("not contain", result.Message);
    }
}
