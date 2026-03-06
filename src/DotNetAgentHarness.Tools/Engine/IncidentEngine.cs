using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DotNetAgentHarness.Tools.Engine;

public static class IncidentEngine
{
    public static PromptIncidentRecord AddPromptIncident(string repoRoot, string title, string promptEvidenceId, PromptIncidentCreateOptions options)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("incident add requires a non-empty title.");
        }

        var report = RepoStateStore.LoadPreparedMessageEvidenceReport(repoRoot, promptEvidenceId);
        var createdAtUtc = DateTimeOffset.UtcNow;
        var incidentId = string.IsNullOrWhiteSpace(options.IncidentId)
            ? $"{createdAtUtc:yyyyMMddHHmmss}-{Slugify(title)}"
            : options.IncidentId.Trim();
        var severity = NormalizeSeverity(options.Severity);

        return CreateIncidentRecord(
            repoRoot,
            incidentId,
            title.Trim(),
            severity,
            options.Owner.Trim(),
            options.Notes.Trim(),
            createdAtUtc,
            report,
            evalContext: null);
    }

    public static IReadOnlyList<PromptIncidentRecord> ListIncidents(string repoRoot)
    {
        return RepoStateStore.ListIncidentRecords(repoRoot);
    }

    public static PromptIncidentRecord ShowIncident(string repoRoot, string incidentId)
    {
        return RepoStateStore.LoadIncidentRecord(repoRoot, incidentId);
    }

    public static PromptIncidentRecord ResolveIncident(string repoRoot, string incidentId, PromptIncidentResolutionOptions options)
    {
        return UpdateIncidentStatus(repoRoot, incidentId, "resolved", options);
    }

    public static PromptIncidentRecord CloseIncident(string repoRoot, string incidentId, PromptIncidentResolutionOptions options)
    {
        return UpdateIncidentStatus(repoRoot, incidentId, "closed", options);
    }

    public static PromptIncidentRecord AddPromptIncidentFromEvalArtifact(string repoRoot, string evalArtifactPathOrId, PromptIncidentCreateOptions options)
    {
        var artifact = LoadEvalArtifact(repoRoot, evalArtifactPathOrId);
        if (artifact.Overall.FailedTrials <= 0)
        {
            throw new ArgumentException("Eval artifact does not contain any failed trials to convert into an incident.");
        }

        var promptEvidenceId = options.PromptEvidenceId
            ?? artifact.PromptEvidenceId
            ?? throw new ArgumentException("Eval artifact did not include prompt evidence. Pass --prompt-evidence <id>.");

        var report = RepoStateStore.LoadPreparedMessageEvidenceReport(repoRoot, promptEvidenceId);
        var createdAtUtc = DateTimeOffset.UtcNow;
        var failedCases = artifact.Cases
            .Where(item => item.FailedTrials > 0)
            .Select(item => new EvalIncidentCaseFailure
            {
                CaseId = item.CaseId,
                Description = item.Description,
                ExpectedTrigger = item.ExpectedTrigger,
                PassRate = item.PassRate,
                FailedTrials = item.FailedTrials,
                FailureSummary = item.Failures.FirstOrDefault()?.Summary ?? $"{item.FailedTrials} trial(s) failed."
            })
            .ToList();

        var title = failedCases.Count == 1
            ? $"Eval failure {failedCases[0].CaseId}"
            : $"Eval failures {artifact.RunId}";
        var incidentId = string.IsNullOrWhiteSpace(options.IncidentId)
            ? $"{createdAtUtc:yyyyMMddHHmmss}-{Slugify(title)}"
            : options.IncidentId.Trim();
        var severity = string.IsNullOrWhiteSpace(options.Severity) || options.Severity.Equals("medium", StringComparison.OrdinalIgnoreCase)
            ? InferEvalSeverity(artifact)
            : NormalizeSeverity(options.Severity);

        var evalContext = new EvalIncidentContext
        {
            ArtifactPath = artifact.SourcePath,
            RunId = artifact.RunId,
            Gate = artifact.Gate,
            PolicyProfile = artifact.PolicyProfile,
            Provider = artifact.Provider,
            Model = artifact.Model,
            CaseFilePath = artifact.CaseFilePath,
            UseDummyMode = artifact.UseDummyMode,
            TotalCases = artifact.Overall.CaseCount,
            TotalTrials = artifact.Overall.TrialCount,
            FailedTrials = artifact.Overall.FailedTrials,
            PassRate = artifact.Overall.PassRate,
            FailedCases = failedCases
        };

        var notes = string.IsNullOrWhiteSpace(options.Notes)
            ? $"Linked from eval artifact '{Path.GetFileName(artifact.SourcePath)}'."
            : options.Notes.Trim();

        return CreateIncidentRecord(
            repoRoot,
            incidentId,
            title,
            severity,
            options.Owner.Trim(),
            notes,
            createdAtUtc,
            report,
            evalContext);
    }

    private static string NormalizeSeverity(string? severity)
    {
        var normalized = (severity ?? "medium").Trim().ToLowerInvariant();
        return normalized switch
        {
            "low" => "low",
            "medium" => "medium",
            "high" => "high",
            "critical" => "critical",
            _ => throw new ArgumentException($"Unsupported severity '{severity}'. Use low, medium, high, or critical.")
        };
    }

    private static string Slugify(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        var previousDash = false;

        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[length++] = ch;
                previousDash = false;
                continue;
            }

            if (previousDash)
            {
                continue;
            }

            buffer[length++] = '-';
            previousDash = true;
        }

        var slug = new string(buffer[..length]).Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "incident" : slug;
    }

    private static PromptIncidentRecord CreateIncidentRecord(
        string repoRoot,
        string incidentId,
        string title,
        string severity,
        string owner,
        string notes,
        DateTimeOffset createdAtUtc,
        PreparedMessageReport report,
        EvalIncidentContext? evalContext)
    {
        var promptEvidence = report.Evidence
            ?? throw new InvalidOperationException("Prompt evidence must be attached before incident creation.");
        var incidentPath = RepoStateStore.ResolveIncidentPath(repoRoot, incidentId);
        var record = new PromptIncidentRecord
        {
            IncidentId = incidentId,
            Title = title,
            Status = "open",
            Severity = severity,
            Owner = owner,
            Notes = notes,
            CreatedAtUtc = createdAtUtc,
            FilePath = incidentPath,
            PromptEvidence = promptEvidence,
            PersonaId = report.Persona.Id,
            Platform = report.RenderedPrompt.Platform,
            Target = report.Target.DisplayPath,
            RawRequest = report.RawRequest,
            EnhancedRequest = report.EnhancedRequest,
            EvalContext = evalContext,
            Resolution = null
        };

        RepoStateStore.WriteIncident(repoRoot, incidentId, record);
        return record;
    }

    private static PromptIncidentRecord UpdateIncidentStatus(
        string repoRoot,
        string incidentId,
        string targetStatus,
        PromptIncidentResolutionOptions options)
    {
        if (string.IsNullOrWhiteSpace(incidentId))
        {
            throw new ArgumentException("Incident id is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Owner))
        {
            throw new ArgumentException($"incident {targetStatus} requires --owner <name>.");
        }

        if (string.IsNullOrWhiteSpace(options.Rationale))
        {
            throw new ArgumentException($"incident {targetStatus} requires --rationale <text>.");
        }

        if (string.IsNullOrWhiteSpace(options.RegressionCaseId))
        {
            throw new ArgumentException($"incident {targetStatus} requires --regression-case <id>.");
        }

        var existing = RepoStateStore.LoadIncidentRecord(repoRoot, incidentId);
        if (existing.Status.Equals(targetStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Incident '{incidentId}' is already {targetStatus}.");
        }

        var resolution = new PromptIncidentResolution
        {
            Status = targetStatus,
            Owner = options.Owner.Trim(),
            Rationale = options.Rationale.Trim(),
            ResolvedAtUtc = DateTimeOffset.UtcNow,
            RegressionCaseId = options.RegressionCaseId.Trim(),
            RegressionCasePath = options.RegressionCasePath.Trim(),
            Notes = options.Notes.Trim()
        };

        var updated = new PromptIncidentRecord
        {
            IncidentId = existing.IncidentId,
            Title = existing.Title,
            Status = targetStatus,
            Severity = existing.Severity,
            Owner = existing.Owner,
            Notes = existing.Notes,
            CreatedAtUtc = existing.CreatedAtUtc,
            FilePath = existing.FilePath,
            PromptEvidence = existing.PromptEvidence,
            PersonaId = existing.PersonaId,
            Platform = existing.Platform,
            Target = existing.Target,
            RawRequest = existing.RawRequest,
            EnhancedRequest = existing.EnhancedRequest,
            EvalContext = existing.EvalContext,
            Resolution = resolution
        };

        RepoStateStore.WriteIncident(repoRoot, incidentId, updated);
        return updated;
    }

    private static string InferEvalSeverity(LinkedEvalArtifact artifact)
    {
        if (artifact.Gate.Equals("release", StringComparison.OrdinalIgnoreCase)
            || artifact.Gate.Equals("nightly", StringComparison.OrdinalIgnoreCase)
            || artifact.PolicyProfile.Equals("strict", StringComparison.OrdinalIgnoreCase))
        {
            return "high";
        }

        return artifact.Overall.FailedTrials > artifact.Overall.TrialCount / 2
            ? "high"
            : "medium";
    }

    private static LinkedEvalArtifact LoadEvalArtifact(string repoRoot, string evalArtifactPathOrId)
    {
        if (string.IsNullOrWhiteSpace(evalArtifactPathOrId))
        {
            throw new ArgumentException("Eval artifact path is required.");
        }

        var path = ResolveEvalArtifactPath(repoRoot, evalArtifactPathOrId.Trim());
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Eval artifact '{evalArtifactPathOrId}' was not found.", path);
        }

        var artifact = JsonSerializer.Deserialize<LinkedEvalArtifact>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Failed to deserialize eval artifact '{path}'.");

        artifact.SourcePath = path;
        return artifact;
    }

    private static string ResolveEvalArtifactPath(string repoRoot, string input)
    {
        var candidates = new[]
        {
            input,
            Path.IsPathRooted(input) ? input : Path.Combine(repoRoot, input),
            Path.Combine(repoRoot, ".dotnet-agent-harness", "evidence", "evals", input.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? input : $"{input}.json"),
            Path.Combine(repoRoot, "artifacts", "evals", input.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? input : $"{input}.json")
        };

        if (Path.IsPathRooted(input))
        {
            return candidates[1];
        }

        return candidates.FirstOrDefault(File.Exists) ?? candidates[2];
    }

    private sealed class LinkedEvalArtifact
    {
        public string RunId { get; set; } = string.Empty;
        public bool UseDummyMode { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string CaseFilePath { get; set; } = string.Empty;
        public string Gate { get; set; } = string.Empty;
        public string PolicyProfile { get; set; } = string.Empty;
        public string? PromptEvidenceId { get; set; }
        public LinkedEvalOverall Overall { get; set; } = new();
        public System.Collections.Generic.List<LinkedEvalCase> Cases { get; set; } = [];
        public string SourcePath { get; set; } = string.Empty;
    }

    private sealed class LinkedEvalOverall
    {
        public int CaseCount { get; set; }
        public int TrialCount { get; set; }
        public int FailedTrials { get; set; }
        public double PassRate { get; set; }
    }

    private sealed class LinkedEvalCase
    {
        public string CaseId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExpectedTrigger { get; set; } = string.Empty;
        public double PassRate { get; set; }
        public int FailedTrials { get; set; }
        public System.Collections.Generic.List<LinkedEvalFailure> Failures { get; set; } = [];
    }

    private sealed class LinkedEvalFailure
    {
        public string Summary { get; set; } = string.Empty;
    }
}
