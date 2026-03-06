using System;
using System.Collections.Generic;

namespace DotNetAgentHarness.Tools.Engine;

public sealed class PersonaDefinition
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public string RiskTier { get; init; } = "general";
    public string? DefaultSubagent { get; init; }
    public List<string> PreferredSubagents { get; init; } = new();
    public List<string> DefaultSkills { get; init; } = new();
    public List<string> AllowedTools { get; init; } = new();
    public List<string> ForbiddenTools { get; init; } = new();
    public List<string> OutputContract { get; init; } = new();
    public List<string> IntentSignals { get; init; } = new();
    public List<string> SystemDirectives { get; init; } = new();
    public List<string> ToolDirectives { get; init; } = new();
    public List<string> RequestDirectives { get; init; } = new();
}

public sealed class PersonaCatalog
{
    public List<PersonaDefinition> Personas { get; init; } = new();

    public PersonaDefinition? Find(string id)
    {
        return Personas.Find(persona => persona.Id.Equals(id, System.StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class PromptAssemblyOptions
{
    public string? PersonaId { get; init; }
    public string? TargetPath { get; init; }
    public int SkillLimit { get; init; } = 6;
    public string Platform { get; init; } = PromptPlatforms.Generic;
}

public sealed class RepoTargetSelection
{
    public string? TargetPath { get; init; }
    public string DisplayPath { get; init; } = string.Empty;
    public bool IsExplicit { get; init; }
    public bool IsAmbiguous { get; init; }
    public string Resolution { get; init; } = string.Empty;
    public List<string> Candidates { get; init; } = new();
}

public sealed class PromptBundle
{
    public string SystemLayer { get; init; } = string.Empty;
    public string ToolLayer { get; init; } = string.Empty;
    public string SkillLayer { get; init; } = string.Empty;
    public string RequestLayer { get; init; } = string.Empty;
}

public sealed class PromptMessage
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public sealed class RenderedPrompt
{
    public string Platform { get; init; } = PromptPlatforms.Generic;
    public List<PromptMessage> Messages { get; init; } = new();
    public string CompositeText { get; init; } = string.Empty;
}

public sealed class PreparedCatalogSelection
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public List<string> Reasons { get; init; } = new();
    public string Source { get; init; } = string.Empty;
}

public sealed class PreparedMessageReport
{
    public string RawRequest { get; init; } = string.Empty;
    public string EnhancedRequest { get; init; } = string.Empty;
    public RepositoryProfile Profile { get; init; } = new();
    public DoctorReport Doctor { get; init; } = new();
    public PersonaDefinition Persona { get; init; } = new();
    public RepoTargetSelection Target { get; init; } = new();
    public List<string> Risks { get; init; } = new();
    public List<PreparedCatalogSelection> Skills { get; init; } = new();
    public PreparedCatalogSelection? Subagent { get; init; }
    public RecommendationBundle Recommendations { get; init; } = new();
    public PromptBundle Bundle { get; init; } = new();
    public RenderedPrompt RenderedPrompt { get; init; } = new();
    public PreparedMessageEvidence? Evidence { get; set; }
}

public static class PromptPlatforms
{
    public const string Generic = "generic";
    public const string CodexCli = "codexcli";
    public const string ClaudeCode = "claudecode";
    public const string OpenCode = "opencode";
    public const string GeminiCli = "geminicli";
    public const string Copilot = "copilot";
    public const string Antigravity = "antigravity";
}

public sealed class PreparedMessageEvidence
{
    public string EvidenceId { get; init; } = string.Empty;
    public string ReportPath { get; init; } = string.Empty;
    public string PromptPath { get; init; } = string.Empty;
}

public sealed class PromptIncidentRecord
{
    public string IncidentId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = "open";
    public string Severity { get; init; } = "medium";
    public string Owner { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public PreparedMessageEvidence PromptEvidence { get; init; } = new();
    public string PersonaId { get; init; } = string.Empty;
    public string Platform { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string RawRequest { get; init; } = string.Empty;
    public string EnhancedRequest { get; init; } = string.Empty;
    public EvalIncidentContext? EvalContext { get; init; }
    public PromptIncidentResolution? Resolution { get; init; }
}

public sealed class PromptIncidentCreateOptions
{
    public string? IncidentId { get; init; }
    public string Severity { get; init; } = "medium";
    public string Owner { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string? PromptEvidenceId { get; init; }
}

public sealed class PromptIncidentResolution
{
    public string Status { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public DateTimeOffset ResolvedAtUtc { get; init; }
    public string RegressionCaseId { get; init; } = string.Empty;
    public string RegressionCasePath { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public sealed class PromptIncidentResolutionOptions
{
    public string Owner { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public string RegressionCaseId { get; init; } = string.Empty;
    public string RegressionCasePath { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public sealed class PromptSectionComparison
{
    public string SectionName { get; init; } = string.Empty;
    public bool IsIdentical { get; init; }
    public int LeftLineCount { get; init; }
    public int RightLineCount { get; init; }
    public int? FirstDifferenceLine { get; init; }
    public List<string> LeftOnlyLines { get; init; } = new();
    public List<string> RightOnlyLines { get; init; } = new();
}

public sealed class PromptComparisonReport
{
    public string LeftEvidenceId { get; init; } = string.Empty;
    public string RightEvidenceId { get; init; } = string.Empty;
    public string LeftReportPath { get; init; } = string.Empty;
    public string RightReportPath { get; init; } = string.Empty;
    public string LeftPersonaId { get; init; } = string.Empty;
    public string RightPersonaId { get; init; } = string.Empty;
    public string LeftPlatform { get; init; } = string.Empty;
    public string RightPlatform { get; init; } = string.Empty;
    public string LeftTarget { get; init; } = string.Empty;
    public string RightTarget { get; init; } = string.Empty;
    public bool SamePersona { get; init; }
    public bool SamePlatform { get; init; }
    public List<PromptSectionComparison> Sections { get; init; } = new();
    public List<string> ChangedSections { get; init; } = new();
}

public sealed class EvalIncidentContext
{
    public string ArtifactPath { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    public string Gate { get; init; } = string.Empty;
    public string PolicyProfile { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string CaseFilePath { get; init; } = string.Empty;
    public bool UseDummyMode { get; init; }
    public int TotalCases { get; init; }
    public int TotalTrials { get; init; }
    public int FailedTrials { get; init; }
    public double PassRate { get; init; }
    public List<EvalIncidentCaseFailure> FailedCases { get; init; } = new();
}

public sealed class EvalIncidentCaseFailure
{
    public string CaseId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ExpectedTrigger { get; init; } = string.Empty;
    public double PassRate { get; init; }
    public int FailedTrials { get; init; }
    public string FailureSummary { get; init; } = string.Empty;
}
