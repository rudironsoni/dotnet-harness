using System;
using System.Collections.Generic;
using System.Linq;
using DotNetAgentHarness.Evals.Models;

namespace DotNetAgentHarness.Evals.Engine;

public class AssertionResult
{
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
}

public static class AssertionRunner
{
    public static AssertionResult Evaluate(string responseContent, Assertion assertion)
    {
        return assertion.Type.ToLowerInvariant() switch
        {
            "contains" => BuildContainsResult(responseContent, assertion.Value),
            "not_contains" => BuildNotContainsResult(responseContent, assertion.Value),
            // Tier 2 (LLM Judge) implementation deferred for the full loop integration
            _ => throw new InvalidOperationException($"Assertion type '{assertion.Type}' not recognized.")
        };
    }

    private static AssertionResult BuildContainsResult(string responseContent, string expectedFragment)
    {
        var passed = responseContent.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase);
        var message = passed
            ? $"Response contains '{expectedFragment}'."
            : $"Expected response to contain '{expectedFragment}', but it was missing.";

        return new AssertionResult { Passed = passed, Message = message };
    }

    private static AssertionResult BuildNotContainsResult(string responseContent, string forbiddenFragment)
    {
        var passed = !responseContent.Contains(forbiddenFragment, StringComparison.OrdinalIgnoreCase);
        var message = passed
            ? $"Response does not contain '{forbiddenFragment}'."
            : $"Expected response to not contain '{forbiddenFragment}', but it was present.";

        return new AssertionResult { Passed = passed, Message = message };
    }
}
