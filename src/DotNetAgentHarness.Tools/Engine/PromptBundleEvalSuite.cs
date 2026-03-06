using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNetAgentHarness.Tools.Engine;

public static class PromptBundleEvalSuite
{
    public static IReadOnlyList<ValidationCheck> Run(string repoRoot)
    {
        var checks = new List<ValidationCheck>();
        var profile = ProjectAnalyzer.Analyze(repoRoot);
        var target = ResolveEvaluationTarget(profile);

        checks.Add(Evaluate(
            "prompt-persona-precedence",
            () =>
            {
                var report = PromptBundleBuilder.Prepare(repoRoot, "Review this implementation for risks", new PromptAssemblyOptions
                {
                    PersonaId = "implementer",
                    TargetPath = target,
                    Platform = PromptPlatforms.Generic
                });

                return (
                    report.Persona.Id.Equals("implementer", StringComparison.OrdinalIgnoreCase),
                    $"Resolved persona '{report.Persona.Id}' for an explicit implementer override.",
                    $"expected=implementer actual={report.Persona.Id}");
            }));

        checks.Add(Evaluate(
            "prompt-tool-policy",
            () =>
            {
                var reviewer = PromptBundleBuilder.Prepare(repoRoot, "Review the validation pipeline for regressions", new PromptAssemblyOptions
                {
                    PersonaId = "reviewer",
                    TargetPath = target,
                    Platform = PromptPlatforms.ClaudeCode
                });
                var implementer = PromptBundleBuilder.Prepare(repoRoot, "Implement a targeted validation improvement", new PromptAssemblyOptions
                {
                    PersonaId = "implementer",
                    TargetPath = target,
                    Platform = PromptPlatforms.CodexCli
                });

                var passed = reviewer.Bundle.ToolLayer.Contains("Forbidden tools: Edit, Write", StringComparison.Ordinal)
                             && reviewer.RenderedPrompt.CompositeText.Contains("Forbidden tools: Edit, Write", StringComparison.Ordinal)
                             && !implementer.Bundle.ToolLayer.Contains("Forbidden tools:", StringComparison.Ordinal);
                var evidence = $"reviewer-forbidden={reviewer.Bundle.ToolLayer.Contains("Forbidden tools:", StringComparison.Ordinal)}; implementer-forbidden={implementer.Bundle.ToolLayer.Contains("Forbidden tools:", StringComparison.Ordinal)}";
                return (passed, "Reviewer stays read-only while implementer remains editable.", evidence);
            }));

        checks.Add(Evaluate(
            "prompt-platform-rendering",
            () =>
            {
                var codex = PromptBundleBuilder.Prepare(repoRoot, "Implement a safer repo validation summary", new PromptAssemblyOptions
                {
                    PersonaId = "implementer",
                    TargetPath = target,
                    Platform = "codex"
                });
                var copilot = PromptBundleBuilder.Prepare(repoRoot, "Review repo doctor output", new PromptAssemblyOptions
                {
                    PersonaId = "reviewer",
                    TargetPath = target,
                    Platform = PromptPlatforms.Copilot
                });
                var gemini = PromptBundleBuilder.Prepare(repoRoot, "Review repo doctor output", new PromptAssemblyOptions
                {
                    PersonaId = "reviewer",
                    TargetPath = target,
                    Platform = PromptPlatforms.GeminiCli
                });
                var antigravity = PromptBundleBuilder.Prepare(repoRoot, "Implement a safer repo validation summary", new PromptAssemblyOptions
                {
                    PersonaId = "implementer",
                    TargetPath = target,
                    Platform = PromptPlatforms.Antigravity
                });

                var passed = codex.RenderedPrompt.Platform == PromptPlatforms.CodexCli
                             && codex.RenderedPrompt.Messages.Count == 2
                             && codex.RenderedPrompt.Messages[0].Role == "system"
                             && codex.RenderedPrompt.Messages[1].Role == "user"
                             && copilot.RenderedPrompt.CompositeText.Contains("Platform: copilot", StringComparison.Ordinal)
                             && gemini.RenderedPrompt.CompositeText.Contains("SYSTEM INSTRUCTIONS", StringComparison.Ordinal)
                             && antigravity.RenderedPrompt.CompositeText.Contains("MISSION", StringComparison.Ordinal);
                var evidence = $"codex-platform={codex.RenderedPrompt.Platform}; codex-messages={codex.RenderedPrompt.Messages.Count}; copilot-platform={copilot.RenderedPrompt.Platform}; gemini-platform={gemini.RenderedPrompt.Platform}; antigravity-platform={antigravity.RenderedPrompt.Platform}";
                return (passed, "Prompt renderer normalizes aliases and preserves the two-message envelope.", evidence);
            }));

        return checks;
    }

    private static string? ResolveEvaluationTarget(RepositoryProfile profile)
    {
        if (profile.Solutions.Count > 0)
        {
            return profile.Solutions[0];
        }

        return profile.Projects
            .Where(project => !project.IsTestProject)
            .Select(project => project.RelativePath)
            .FirstOrDefault()
            ?? profile.Projects.Select(project => project.RelativePath).FirstOrDefault();
    }

    private static ValidationCheck Evaluate(string name, Func<(bool Passed, string Message, string Evidence)> assertion)
    {
        try
        {
            var result = assertion();
            return new ValidationCheck
            {
                Name = name,
                Passed = result.Passed,
                Severity = result.Passed ? "info" : "error",
                Message = result.Message,
                Evidence = result.Evidence
            };
        }
        catch (Exception ex)
        {
            return new ValidationCheck
            {
                Name = name,
                Passed = false,
                Severity = "error",
                Message = ex.Message
            };
        }
    }
}
