using System.Collections.Generic;

namespace DotNetAgentHarness.Tools.Engine;

public sealed class ReviewReport
{
    public string RepoRoot { get; init; } = string.Empty;
    public int ScannedFiles { get; init; }
    public List<ReviewFinding> Findings { get; init; } = new();
}

public sealed class ReviewFinding
{
    public string Severity { get; init; } = string.Empty;
    public string RuleId { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public int LineNumber { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Guidance { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
}

public sealed class SkillTestSuiteResult
{
    public List<SkillTestResult> Skills { get; init; } = new();
    public bool Passed { get; init; }
    public int TotalCases { get; init; }
    public int SkillsWithCases { get; init; }
    public int SkillsWithoutCases { get; init; }
    public int TotalChecks { get; init; }
    public int FailedChecks { get; init; }
}

public sealed class SkillTestResult
{
    public string SkillId { get; init; } = string.Empty;
    public string SkillPath { get; init; } = string.Empty;
    public int CaseCount { get; init; }
    public List<SkillTestCheck> Checks { get; init; } = new();
    public bool Passed { get; init; }
}

public sealed class SkillTestCheck
{
    public string Name { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Message { get; init; } = string.Empty;
    public string SourceFile { get; init; } = string.Empty;
    public string CaseName { get; init; } = string.Empty;
    public string TestName { get; init; } = string.Empty;
}

public sealed class SkillTestCaseDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public List<SkillTestCommandDefinition> SetupCommands { get; set; } = new();
    public List<SkillTestDefinition> Tests { get; set; } = new();
    public List<SkillTestCommandDefinition> TeardownCommands { get; set; } = new();
}

public sealed class SkillTestCommandDefinition
{
    public string Command { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 60_000;
    public bool ContinueOnError { get; set; }
}

public sealed class SkillTestDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 60_000;
    public SkillTestExpectation Expected { get; set; } = new();
}

public sealed class SkillTestExpectation
{
    public string Status { get; set; } = string.Empty;
    public List<string> OutputContains { get; set; } = new();
    public List<string> OutputMatches { get; set; } = new();
    public List<string> ErrorContains { get; set; } = new();
    public List<string> FileExists { get; set; } = new();
    public List<string> FileNotExists { get; set; } = new();
    public List<string> SkillContains { get; set; } = new();
    public List<string> SkillMatches { get; set; } = new();
    public Dictionary<string, string> Frontmatter { get; set; } = new();
    public bool NoErrors { get; set; }
}

public sealed class ProcessExecutionResult
{
    public string Command { get; init; } = string.Empty;
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool TimedOut { get; init; }
}

public sealed class ScaffoldTemplate
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? DotNetTemplate { get; init; }
}

public sealed class ScaffoldPlan
{
    public string TemplateId { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string SolutionName { get; init; } = string.Empty;
    public List<ScaffoldStep> Steps { get; init; } = new();
}

public sealed class ScaffoldStep
{
    public string Kind { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
}
