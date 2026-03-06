using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DotNetAgentHarness.Tools.Engine;

public static class RepoStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly string PreparedMessagesRoot = Path.Combine(".dotnet-agent-harness", "evidence", "prepared-messages");
    private static readonly string IncidentsRoot = Path.Combine(".dotnet-agent-harness", "incidents");

    public static string WriteJson(string repoRoot, string relativePath, object value)
    {
        var fullPath = Path.Combine(repoRoot, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, JsonSerializer.Serialize(value, JsonOptions));
        return fullPath;
    }

    public static PreparedMessageEvidence WritePreparedMessageEvidence(string repoRoot, PreparedMessageReport report, string? evidenceId = null)
    {
        var resolvedId = string.IsNullOrWhiteSpace(evidenceId)
            ? $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{report.Persona.Id}-{report.RenderedPrompt.Platform}"
            : evidenceId;

        var safeId = SanitizePathSegment(resolvedId);
        var reportPath = Path.Combine(repoRoot, PreparedMessagesRoot, $"{safeId}.json");
        var promptPath = Path.Combine(repoRoot, PreparedMessagesRoot, $"{safeId}.prompt.txt");
        var directory = Path.GetDirectoryName(promptPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var evidence = new PreparedMessageEvidence
        {
            EvidenceId = safeId,
            ReportPath = reportPath,
            PromptPath = promptPath
        };

        report.Evidence = evidence;
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        File.WriteAllText(promptPath, report.RenderedPrompt.CompositeText);
        return evidence;
    }

    public static PreparedMessageReport LoadPreparedMessageEvidenceReport(string repoRoot, string evidenceId)
    {
        var evidence = ResolvePreparedMessageEvidence(repoRoot, evidenceId);
        var report = JsonSerializer.Deserialize<PreparedMessageReport>(File.ReadAllText(evidence.ReportPath), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize prepared-message report '{evidence.ReportPath}'.");

        report.Evidence ??= evidence;
        return report;
    }

    public static PreparedMessageEvidence ResolvePreparedMessageEvidence(string repoRoot, string evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            throw new ArgumentException("Prompt evidence id is required.");
        }

        var normalizedId = evidenceId.Trim();
        var reportPath = ResolveExistingPath(repoRoot, normalizedId, ".json");
        var promptPath = ResolveExistingPath(repoRoot, normalizedId, ".prompt.txt");
        if (!File.Exists(reportPath))
        {
            throw new FileNotFoundException($"Prepared-message report not found for evidence id '{evidenceId}'.", reportPath);
        }

        if (!File.Exists(promptPath))
        {
            throw new FileNotFoundException($"Prepared-message prompt not found for evidence id '{evidenceId}'.", promptPath);
        }

        return new PreparedMessageEvidence
        {
            EvidenceId = Path.GetFileNameWithoutExtension(reportPath),
            ReportPath = reportPath,
            PromptPath = promptPath
        };
    }

    public static string WriteIncident(string repoRoot, string incidentId, object record)
    {
        var safeId = SanitizePathSegment(incidentId);
        return WriteJson(repoRoot, Path.Combine(IncidentsRoot, $"{safeId}.json"), record);
    }

    public static string ResolveIncidentPath(string repoRoot, string incidentId)
    {
        if (string.IsNullOrWhiteSpace(incidentId))
        {
            throw new ArgumentException("Incident id is required.");
        }

        return ResolveExistingPath(repoRoot, incidentId.Trim(), ".json", IncidentsRoot);
    }

    public static IReadOnlyList<PromptIncidentRecord> ListIncidentRecords(string repoRoot)
    {
        var root = Path.Combine(repoRoot, IncidentsRoot);
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(LoadIncidentRecordFromFile)
            .ToList();
    }

    public static PromptIncidentRecord LoadIncidentRecord(string repoRoot, string incidentId)
    {
        var path = ResolveIncidentPath(repoRoot, incidentId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Incident '{incidentId}' was not found.", path);
        }

        return LoadIncidentRecordFromFile(path);
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var chars = value
            .Trim()
            .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray();

        var sanitized = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "prepared-message" : sanitized;
    }

    private static PromptIncidentRecord LoadIncidentRecordFromFile(string path)
    {
        var record = JsonSerializer.Deserialize<PromptIncidentRecord>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize incident record '{path}'.");

        return new PromptIncidentRecord
        {
            IncidentId = record.IncidentId,
            Title = record.Title,
            Status = string.IsNullOrWhiteSpace(record.Status) ? "open" : record.Status,
            Severity = record.Severity,
            Owner = record.Owner,
            Notes = record.Notes,
            CreatedAtUtc = record.CreatedAtUtc,
            FilePath = string.IsNullOrWhiteSpace(record.FilePath) ? path : record.FilePath,
            PromptEvidence = record.PromptEvidence,
            PersonaId = record.PersonaId,
            Platform = record.Platform,
            Target = record.Target,
            RawRequest = record.RawRequest,
            EnhancedRequest = record.EnhancedRequest,
            EvalContext = record.EvalContext,
            Resolution = record.Resolution
        };
    }

    private static string ResolveExistingPath(string repoRoot, string input, string defaultExtension, string relativeRoot = "")
    {
        var candidates = new[]
        {
            input,
            Path.IsPathRooted(input) ? input : Path.Combine(repoRoot, input),
            Path.Combine(repoRoot, string.IsNullOrWhiteSpace(relativeRoot) ? PreparedMessagesRoot : relativeRoot, input.EndsWith(defaultExtension, StringComparison.OrdinalIgnoreCase) ? input : $"{input}{defaultExtension}")
        };

        return candidates.FirstOrDefault(File.Exists)
               ?? candidates[^1];
    }
}
