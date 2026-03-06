using System.Collections.Generic;

namespace DotNetAgentHarness.Evals.Models;

public class EvalCase
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string ExpectedTrigger { get; set; } = string.Empty;
    public int? TrialCount { get; set; }
    public string FixtureResponse { get; set; } = string.Empty;
    public string FixtureTrigger { get; set; } = string.Empty;
    public List<Assertion> Assertions { get; set; } = new();
}

public class Assertion
{
    public string Type { get; set; } = string.Empty; // "contains", "not_contains", "llm_judge"
    public string Value { get; set; } = string.Empty;
}
