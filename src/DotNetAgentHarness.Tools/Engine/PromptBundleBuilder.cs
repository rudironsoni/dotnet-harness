using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DotNetAgentHarness.Tools.Engine;

public static class PromptBundleBuilder
{
    public static PreparedMessageReport Prepare(string repoRoot, string rawRequest, PromptAssemblyOptions options)
    {
        if (string.IsNullOrWhiteSpace(rawRequest))
        {
            throw new ArgumentException("prepare-message requires a non-empty request.");
        }

        var profile = ProjectAnalyzer.Analyze(repoRoot);
        var doctor = DoctorEngine.BuildReport(profile);
        var catalog = ToolkitCatalogLoader.Load(repoRoot);
        var personas = PersonaCatalogLoader.Load(repoRoot);
        var recommendations = RecommendationEngine.Recommend(profile, catalog, Math.Max(1, options.SkillLimit));
        var target = RepoTargetResolver.Resolve(repoRoot, profile, options.TargetPath);
        var persona = ResolvePersona(personas, rawRequest, options.PersonaId);
        var skills = SelectSkills(persona, recommendations, catalog, options.SkillLimit);
        var subagent = SelectSubagent(persona, recommendations, catalog);
        var risks = BuildRisks(doctor, target);
        var enhancedRequest = BuildEnhancedRequest(rawRequest, profile, target, persona, skills, subagent, risks);
        var bundle = BuildPromptBundle(target, persona, skills, subagent, enhancedRequest);
        var renderedPrompt = PromptBundleRenderer.Render(options.Platform, bundle);

        return new PreparedMessageReport
        {
            RawRequest = rawRequest,
            EnhancedRequest = enhancedRequest,
            Profile = profile,
            Doctor = doctor,
            Persona = persona,
            Target = target,
            Risks = risks,
            Skills = skills,
            Subagent = subagent,
            Recommendations = recommendations,
            Bundle = bundle,
            RenderedPrompt = renderedPrompt
        };
    }

    private static PersonaDefinition ResolvePersona(PersonaCatalog personas, string rawRequest, string? explicitPersonaId)
    {
        if (!string.IsNullOrWhiteSpace(explicitPersonaId))
        {
            return personas.Find(explicitPersonaId)
                ?? throw new ArgumentException($"Persona '{explicitPersonaId}' was not found.");
        }

        var request = rawRequest.ToLowerInvariant();
        return personas.Personas
            .Select(persona => new
            {
                Persona = persona,
                Score = ScorePersona(persona, request)
            })
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Persona.Id, StringComparer.OrdinalIgnoreCase)
            .Select(result => result.Persona)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No personas were available for prompt assembly.");
    }

    private static int ScorePersona(PersonaDefinition persona, string request)
    {
        var score = 0;
        foreach (var signal in persona.IntentSignals)
        {
            if (string.IsNullOrWhiteSpace(signal))
            {
                continue;
            }

            if (request.Contains(signal, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }
        }

        if (persona.Id.Equals("implementer", StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
        }

        return score;
    }

    private static List<PreparedCatalogSelection> SelectSkills(PersonaDefinition persona, RecommendationBundle recommendations, ToolkitCatalog catalog, int limit)
    {
        var selections = new Dictionary<string, PreparedCatalogSelection>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<PreparedCatalogSelection>();

        foreach (var skillId in persona.DefaultSkills)
        {
            var item = catalog.Find(skillId);
            if (item is null || item.Kind != CatalogKinds.Skill)
            {
                continue;
            }

            var selection = new PreparedCatalogSelection
            {
                Id = item.Id,
                Name = item.Name,
                Kind = item.Kind,
                FilePath = item.FilePath,
                Source = "persona-default",
                Reasons = new List<string> { $"Required by persona '{persona.Id}'." }
            };
            selections[item.Id] = selection;
            ordered.Add(selection);
        }

        foreach (var recommendation in recommendations.Skills)
        {
            if (selections.TryGetValue(recommendation.Id, out var existing))
            {
                existing.Reasons.AddRange(recommendation.Reasons.Where(reason => !existing.Reasons.Contains(reason, StringComparer.OrdinalIgnoreCase)));
                continue;
            }

            if (ordered.Count >= Math.Max(limit, persona.DefaultSkills.Count))
            {
                break;
            }

            ordered.Add(new PreparedCatalogSelection
            {
                Id = recommendation.Id,
                Name = recommendation.Name,
                Kind = recommendation.Kind,
                FilePath = recommendation.FilePath,
                Source = "recommendation",
                Reasons = recommendation.Reasons
            });
            selections[recommendation.Id] = ordered[^1];
        }

        return ordered;
    }

    private static PreparedCatalogSelection? SelectSubagent(PersonaDefinition persona, RecommendationBundle recommendations, ToolkitCatalog catalog)
    {
        if (!string.IsNullOrWhiteSpace(persona.DefaultSubagent))
        {
            var item = catalog.Find(persona.DefaultSubagent);
            if (item is not null && item.Kind == CatalogKinds.Subagent)
            {
                return new PreparedCatalogSelection
                {
                    Id = item.Id,
                    Name = item.Name,
                    Kind = item.Kind,
                    FilePath = item.FilePath,
                    Source = "persona-default",
                    Reasons = new List<string> { $"Preferred subagent for persona '{persona.Id}'." }
                };
            }
        }

        foreach (var preferred in persona.PreferredSubagents)
        {
            var recommended = recommendations.Subagents.FirstOrDefault(item => item.Id.Equals(preferred, StringComparison.OrdinalIgnoreCase));
            if (recommended is null)
            {
                continue;
            }

            return new PreparedCatalogSelection
            {
                Id = recommended.Id,
                Name = recommended.Name,
                Kind = recommended.Kind,
                FilePath = recommended.FilePath,
                Source = "recommendation",
                Reasons = recommended.Reasons
            };
        }

        var fallback = recommendations.Subagents.FirstOrDefault();
        if (fallback is null)
        {
            return null;
        }

        return new PreparedCatalogSelection
        {
            Id = fallback.Id,
            Name = fallback.Name,
            Kind = fallback.Kind,
            FilePath = fallback.FilePath,
            Source = "recommendation",
            Reasons = fallback.Reasons
        };
    }

    private static List<string> BuildRisks(DoctorReport doctor, RepoTargetSelection target)
    {
        var risks = doctor.Findings
            .Where(finding => finding.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)
                           || finding.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase))
            .Select(finding => $"[{finding.Severity}] {finding.Code}: {finding.Message}")
            .Take(5)
            .ToList();

        if (string.IsNullOrWhiteSpace(target.TargetPath))
        {
            risks.Add(target.Candidates.Count > 0
                ? $"[warning] target-resolution: {target.Resolution} Candidates: {string.Join(", ", target.Candidates)}."
                : $"[warning] target-resolution: {target.Resolution}");
        }

        return risks;
    }

    private static string BuildEnhancedRequest(
        string rawRequest,
        RepositoryProfile profile,
        RepoTargetSelection target,
        PersonaDefinition persona,
        IReadOnlyList<PreparedCatalogSelection> skills,
        PreparedCatalogSelection? subagent,
        IReadOnlyList<string> risks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("User goal:");
        builder.AppendLine(rawRequest.Trim());
        var taskBrief = RewriteTaskBrief(rawRequest, persona, target);

        if (!string.Equals(taskBrief, rawRequest.Trim(), StringComparison.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine("Prepared task:");
            builder.AppendLine(taskBrief);
        }

        builder.AppendLine();
        builder.AppendLine("Execution context:");
        builder.AppendLine($"- persona: {persona.Id}");
        builder.AppendLine($"- dominant project kind: {profile.DominantProjectKind}");
        builder.AppendLine($"- target frameworks: {FormatList(profile.TargetFrameworks)}");
        builder.AppendLine($"- technologies: {FormatList(profile.Technologies)}");
        builder.AppendLine($"- target: {(string.IsNullOrWhiteSpace(target.DisplayPath) ? "unresolved" : target.DisplayPath)}");
        builder.AppendLine($"- resolution: {target.Resolution}");
        builder.AppendLine();
        builder.AppendLine("Load these skills first:");
        foreach (var skill in skills)
        {
            builder.AppendLine($"- {skill.Id}: {string.Join("; ", skill.Reasons)}");
        }

        if (subagent is not null)
        {
            builder.AppendLine();
            builder.AppendLine($"Preferred subagent: {subagent.Id}");
        }

        if (risks.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Repository risks:");
            foreach (var risk in risks)
            {
                builder.AppendLine($"- {risk}");
            }
        }

        if (persona.OutputContract.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Output contract:");
            foreach (var requirement in persona.OutputContract)
            {
                builder.AppendLine($"- {requirement}");
            }
        }

        if (persona.RequestDirectives.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Request directives:");
            foreach (var directive in persona.RequestDirectives)
            {
                builder.AppendLine($"- {directive}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static PromptBundle BuildPromptBundle(
        RepoTargetSelection target,
        PersonaDefinition persona,
        IReadOnlyList<PreparedCatalogSelection> skills,
        PreparedCatalogSelection? subagent,
        string enhancedRequest)
    {
        return new PromptBundle
        {
            SystemLayer = BuildSystemLayer(persona),
            ToolLayer = BuildToolLayer(persona, target),
            SkillLayer = BuildSkillLayer(skills, subagent),
            RequestLayer = enhancedRequest
        };
    }

    private static string BuildSystemLayer(PersonaDefinition persona)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Operate as the '{persona.DisplayName}' persona.");
        builder.AppendLine($"Primary purpose: {persona.Purpose}");
        builder.AppendLine($"Risk tier: {persona.RiskTier}");
        if (persona.SystemDirectives.Count > 0)
        {
            builder.AppendLine("System directives:");
            foreach (var directive in persona.SystemDirectives)
            {
                builder.AppendLine($"- {directive}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildToolLayer(PersonaDefinition persona, RepoTargetSelection target)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Allowed tools: {FormatList(persona.AllowedTools)}");
        if (persona.ForbiddenTools.Count > 0)
        {
            builder.AppendLine($"Forbidden tools: {FormatList(persona.ForbiddenTools)}");
        }

        if (persona.ToolDirectives.Count > 0)
        {
            builder.AppendLine("Tool directives:");
            foreach (var directive in persona.ToolDirectives)
            {
                builder.AppendLine($"- {directive}");
            }
        }

        if (string.IsNullOrWhiteSpace(target.TargetPath))
        {
            builder.AppendLine("Target status: unresolved. Avoid repo-wide edits until a concrete project or solution target is chosen.");
        }
        else
        {
            builder.AppendLine($"Target status: {target.DisplayPath}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSkillLayer(IReadOnlyList<PreparedCatalogSelection> skills, PreparedCatalogSelection? subagent)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Skill load order:");
        foreach (var skill in skills)
        {
            builder.AppendLine($"- {skill.Id}: {string.Join("; ", skill.Reasons)}");
        }

        if (subagent is not null)
        {
            builder.AppendLine($"Preferred subagent: {subagent.Id}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatList(IReadOnlyCollection<string> values)
    {
        return values.Count == 0 ? "none" : string.Join(", ", values);
    }

    private static string RewriteTaskBrief(string rawRequest, PersonaDefinition persona, RepoTargetSelection target)
    {
        var normalizedRequest = rawRequest.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRequest))
        {
            return normalizedRequest;
        }

        var targetScope = string.IsNullOrWhiteSpace(target.DisplayPath) ? "the repository" : target.DisplayPath;
        return persona.Id.ToLowerInvariant() switch
        {
            "reviewer" => $"Review {targetScope} against this request and report evidence-backed findings: {normalizedRequest}",
            "architect" => $"Assess {targetScope} and recommend the best-fit architecture approach for this request: {normalizedRequest}",
            "tester" => $"Build a verification plan for {targetScope} that addresses this request: {normalizedRequest}",
            "implementer" => $"Implement the requested change in {targetScope}: {normalizedRequest}",
            _ => normalizedRequest
        };
    }
}
