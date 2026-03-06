using System;
using System.Collections.Generic;

namespace DotNetAgentHarness.Evals.Models;

public sealed class EvalRunArtifact
{
    public string RunId { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public bool UseDummyMode { get; init; }
    public string ModeSource { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string CaseFilePath { get; init; } = string.Empty;
    public int DefaultTrialCount { get; init; }
    public string Gate { get; init; } = string.Empty;
    public string PolicyProfile { get; init; } = string.Empty;
    public string? PromptEvidenceId { get; init; }
    public EvalArtifactOverall Overall { get; init; } = new();
    public List<EvalArtifactCase> Cases { get; init; } = new();
}

public sealed class EvalArtifactOverall
{
    public int CaseCount { get; init; }
    public int TrialCount { get; init; }
    public int PassedTrials { get; init; }
    public int FailedTrials { get; init; }
    public double PassRate { get; init; }
}

public sealed class EvalArtifactCase
{
    public string CaseId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string ExpectedTrigger { get; init; } = string.Empty;
    public int TrialCount { get; init; }
    public int PassedTrials { get; init; }
    public int FailedTrials { get; init; }
    public double PassRate { get; init; }
    public double AverageElapsedMilliseconds { get; init; }
    public List<EvalArtifactFailure> Failures { get; init; } = new();
}

public sealed class EvalArtifactFailure
{
    public int TrialNumber { get; init; }
    public string TriggerMessage { get; init; } = string.Empty;
    public List<string> AssertionMessages { get; init; } = new();
    public string Summary { get; init; } = string.Empty;
}
