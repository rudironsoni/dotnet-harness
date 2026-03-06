using System;

namespace DotNetAgentHarness.Evals.Engine;

public sealed record TriggerEvaluation(bool Passed, string Message);

public static class TriggerEvaluator
{
    public static TriggerEvaluation Evaluate(string expectedTrigger, string actualTrigger)
    {
        var expected = Normalize(expectedTrigger);
        var actual = Normalize(actualTrigger);

        if (expected.IsNone && actual.IsNone)
        {
            return new TriggerEvaluation(true, "Trigger matched expected 'none'.");
        }

        if (expected.IsNone && !actual.IsNone)
        {
            return new TriggerEvaluation(false, $"Expected no trigger, but model returned '{actual.Value}'.");
        }

        if (!expected.IsNone && actual.IsNone)
        {
            return new TriggerEvaluation(false, $"Expected trigger '{expected.Value}', but model returned no trigger.");
        }

        if (!string.Equals(expected.Value, actual.Value, StringComparison.OrdinalIgnoreCase))
        {
            return new TriggerEvaluation(false, $"Expected trigger '{expected.Value}', but got '{actual.Value}'.");
        }

        return new TriggerEvaluation(true, $"Trigger matched expected value '{expected.Value}'.");
    }

    public static string Display(string trigger)
    {
        var normalized = Normalize(trigger);
        return normalized.IsNone ? "none" : normalized.Value;
    }

    private static NormalizedTrigger Normalize(string rawTrigger)
    {
        if (string.IsNullOrWhiteSpace(rawTrigger))
        {
            return new NormalizedTrigger(true, "none");
        }

        var value = rawTrigger.Trim();
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase)
            || value.Equals("no_trigger", StringComparison.OrdinalIgnoreCase)
            || value.Equals("null", StringComparison.OrdinalIgnoreCase)
            || value.Equals("no-skill", StringComparison.OrdinalIgnoreCase))
        {
            return new NormalizedTrigger(true, "none");
        }

        return new NormalizedTrigger(false, value);
    }

    private sealed record NormalizedTrigger(bool IsNone, string Value);
}
