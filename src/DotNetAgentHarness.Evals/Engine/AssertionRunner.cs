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
            "contains" => new AssertionResult
            {
                Passed = responseContent.Contains(assertion.Value, StringComparison.OrdinalIgnoreCase),
                Message = $"Expected response to contain '{assertion.Value}'"
            },
            "not_contains" => new AssertionResult
            {
                Passed = !responseContent.Contains(assertion.Value, StringComparison.OrdinalIgnoreCase),
                Message = $"Expected response to NOT contain '{assertion.Value}'"
            },
            // Tier 2 (LLM Judge) implementation deferred for the full loop integration
            _ => throw new InvalidOperationException($"Assertion type '{assertion.Type}' not recognized.")
        };
    }
}
