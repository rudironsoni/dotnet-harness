using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNetAgentHarness.Tools.Engine;

public static class PromptComparisonEngine
{
    public static PromptComparisonReport Compare(string repoRoot, string leftEvidenceId, string rightEvidenceId)
    {
        var left = RepoStateStore.LoadPreparedMessageEvidenceReport(repoRoot, leftEvidenceId);
        var right = RepoStateStore.LoadPreparedMessageEvidenceReport(repoRoot, rightEvidenceId);

        var sections = new List<PromptSectionComparison>
        {
            CompareSection("system", left.Bundle.SystemLayer, right.Bundle.SystemLayer),
            CompareSection("tool", left.Bundle.ToolLayer, right.Bundle.ToolLayer),
            CompareSection("skill", left.Bundle.SkillLayer, right.Bundle.SkillLayer),
            CompareSection("request", left.Bundle.RequestLayer, right.Bundle.RequestLayer)
        };

        return new PromptComparisonReport
        {
            LeftEvidenceId = left.Evidence?.EvidenceId ?? leftEvidenceId,
            RightEvidenceId = right.Evidence?.EvidenceId ?? rightEvidenceId,
            LeftReportPath = left.Evidence?.ReportPath ?? string.Empty,
            RightReportPath = right.Evidence?.ReportPath ?? string.Empty,
            LeftPersonaId = left.Persona.Id,
            RightPersonaId = right.Persona.Id,
            LeftPlatform = left.RenderedPrompt.Platform,
            RightPlatform = right.RenderedPrompt.Platform,
            LeftTarget = left.Target.DisplayPath,
            RightTarget = right.Target.DisplayPath,
            SamePersona = left.Persona.Id.Equals(right.Persona.Id, StringComparison.OrdinalIgnoreCase),
            SamePlatform = left.RenderedPrompt.Platform.Equals(right.RenderedPrompt.Platform, StringComparison.OrdinalIgnoreCase),
            Sections = sections,
            ChangedSections = sections
                .Where(section => !section.IsIdentical)
                .Select(section => section.SectionName)
                .ToList()
        };
    }

    private static PromptSectionComparison CompareSection(string sectionName, string left, string right)
    {
        var leftLines = SplitLines(left);
        var rightLines = SplitLines(right);
        var maxLines = Math.Max(leftLines.Count, rightLines.Count);
        int? firstDifferenceLine = null;

        for (var index = 0; index < maxLines; index++)
        {
            var leftLine = index < leftLines.Count ? leftLines[index] : string.Empty;
            var rightLine = index < rightLines.Count ? rightLines[index] : string.Empty;
            if (!leftLine.Equals(rightLine, StringComparison.Ordinal))
            {
                firstDifferenceLine = index + 1;
                break;
            }
        }

        return new PromptSectionComparison
        {
            SectionName = sectionName,
            IsIdentical = left.Equals(right, StringComparison.Ordinal),
            LeftLineCount = leftLines.Count,
            RightLineCount = rightLines.Count,
            FirstDifferenceLine = firstDifferenceLine,
            LeftOnlyLines = leftLines.Except(rightLines, StringComparer.Ordinal).Take(5).ToList(),
            RightOnlyLines = rightLines.Except(leftLines, StringComparer.Ordinal).Take(5).ToList()
        };
    }

    private static List<string> SplitLines(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None)
            .ToList();
    }
}
