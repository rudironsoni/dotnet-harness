using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DotNetAgentHarness.Tools.Engine;

public static class ToolkitCatalogLoader
{
    private static readonly Regex SkillRefRegex = new(@"\[(skill|subagent):([^\]]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TriggerRegex = new(@"Triggers on:\s*(.+?)(?:\.\s*$|\n|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex HeadingRegex = new(@"^#\s+(.+)$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly string[] IgnoredDirectories = { ".git", "bin", "obj", "node_modules", "dist", ".rulesync/cache" };

    public static ToolkitCatalog Load(string repoRoot)
    {
        var rulesyncRoot = Path.Combine(repoRoot, ".rulesync");
        if (!Directory.Exists(rulesyncRoot))
        {
            throw new DirectoryNotFoundException($"RuleSync source directory not found: {rulesyncRoot}");
        }

        var items = new List<CatalogItem>();
        items.AddRange(LoadSkills(repoRoot, Path.Combine(rulesyncRoot, "skills")));
        items.AddRange(LoadMarkdownDirectory(repoRoot, Path.Combine(rulesyncRoot, "subagents"), CatalogKinds.Subagent));
        items.AddRange(LoadMarkdownDirectory(repoRoot, Path.Combine(rulesyncRoot, "commands"), CatalogKinds.Command));
        items.AddRange(LoadPersonas(repoRoot, Path.Combine(rulesyncRoot, "personas")));

        var stats = new CatalogStats
        {
            TotalItems = items.Count,
            Skills = items.Count(item => item.Kind == CatalogKinds.Skill),
            Subagents = items.Count(item => item.Kind == CatalogKinds.Subagent),
            Commands = items.Count(item => item.Kind == CatalogKinds.Command),
            Personas = items.Count(item => item.Kind == CatalogKinds.Persona),
            TotalLines = items.Sum(item => item.LineCount)
        };

        return new ToolkitCatalog
        {
            Items = items.OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase).ToList(),
            Stats = stats
        };
    }

    public static List<CatalogSearchResult> Search(ToolkitCatalog catalog, CatalogSearchQuery query)
    {
        var terms = SplitTerms(query.Query);
        var kindFilter = Normalize(query.Kind);
        var platformFilter = Normalize(query.Platform);
        var categoryFilter = Normalize(query.Category);

        return catalog.Items
            .Where(item => string.IsNullOrWhiteSpace(kindFilter) || item.Kind.Equals(kindFilter, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(platformFilter) || item.Platforms.Contains("*") || item.Platforms.Any(platform => platform.Equals(platformFilter, StringComparison.OrdinalIgnoreCase)))
            .Where(item => string.IsNullOrWhiteSpace(categoryFilter) || MatchesCategory(item, categoryFilter))
            .Select(item => Score(item, terms))
            .Where(result => result.Score > 0 || terms.Count == 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Item.Id, StringComparer.OrdinalIgnoreCase)
            .Take(query.Limit)
            .ToList();
    }

    public static CatalogComparison Compare(ToolkitCatalog catalog, string leftId, string rightId)
    {
        var left = catalog.Find(leftId) ?? throw new InvalidOperationException($"Catalog item '{leftId}' not found.");
        var right = catalog.Find(rightId) ?? throw new InvalidOperationException($"Catalog item '{rightId}' not found.");

        var sharedTags = left.Tags.Intersect(right.Tags, StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToList();
        var uniqueToLeft = left.Tags.Except(right.Tags, StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToList();
        var uniqueToRight = right.Tags.Except(left.Tags, StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToList();

        return new CatalogComparison
        {
            Left = left,
            Right = right,
            SharedTags = sharedTags,
            UniqueToLeft = uniqueToLeft,
            UniqueToRight = uniqueToRight
        };
    }

    private static IEnumerable<CatalogItem> LoadSkills(string repoRoot, string skillsRoot)
    {
        if (!Directory.Exists(skillsRoot))
        {
            yield break;
        }

        foreach (var skillDirectory in Directory.GetDirectories(skillsRoot))
        {
            var skillFile = Path.Combine(skillDirectory, "SKILL.md");
            if (!File.Exists(skillFile))
            {
                continue;
            }

            yield return BuildCatalogItem(repoRoot, skillFile, CatalogKinds.Skill, Path.GetFileName(skillDirectory));
        }
    }

    private static IEnumerable<CatalogItem> LoadMarkdownDirectory(string repoRoot, string directory, string kind)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var filePath in EnumerateMarkdownFiles(directory))
        {
            yield return BuildCatalogItem(repoRoot, filePath, kind, Path.GetFileNameWithoutExtension(filePath));
        }
    }

    private static IEnumerable<CatalogItem> LoadPersonas(string repoRoot, string personasRoot)
    {
        if (!Directory.Exists(personasRoot))
        {
            yield break;
        }

        foreach (var filePath in Directory.EnumerateFiles(personasRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            var persona = PersonaCatalogLoader.LoadPersonaFile(filePath);
            var content = File.ReadAllText(filePath);
            var lineCount = content.Split('\n').Length;
            var tags = new List<string> { "persona", persona.RiskTier };
            tags.AddRange(persona.AllowedTools.Count == 0
                ? []
                : [persona.ForbiddenTools.Count == 0 ? "editable" : "guarded"]);

            var references = persona.DefaultSkills
                .Concat(persona.PreferredSubagents)
                .Concat(string.IsNullOrWhiteSpace(persona.DefaultSubagent) ? [] : [persona.DefaultSubagent])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            yield return new CatalogItem
            {
                Id = persona.Id,
                Name = string.IsNullOrWhiteSpace(persona.DisplayName) ? persona.Id : persona.DisplayName,
                Kind = CatalogKinds.Persona,
                Description = string.IsNullOrWhiteSpace(persona.Description) ? persona.Purpose : persona.Description,
                Tags = tags
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Platforms = ["*"],
                FilePath = Path.GetRelativePath(repoRoot, filePath),
                LineCount = lineCount,
                Triggers = persona.IntentSignals
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                References = references,
                ApproximateTokens = Math.Max(1, content.Length / 4)
            };
        }
    }

    private static CatalogItem BuildCatalogItem(string repoRoot, string filePath, string kind, string fallbackId)
    {
        var content = File.ReadAllText(filePath);
        var lineCount = content.Split('\n').Length;
        var frontmatter = MarkdownFrontmatter.Parse(content);
        var id = MarkdownFrontmatter.GetString(frontmatter, "name") ?? fallbackId;
        var name = ExtractHeading(content) ?? id;
        var description = MarkdownFrontmatter.GetString(frontmatter, "description") ?? string.Empty;
        var tags = MarkdownFrontmatter.GetStringList(frontmatter, "tags");
        var platforms = MarkdownFrontmatter.DetectPlatforms(frontmatter);
        var triggers = ExtractTriggers(description, content);
        var references = SkillRefRegex.Matches(content)
            .Select(match => match.Groups[2].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CatalogItem
        {
            Id = id,
            Name = name,
            Kind = kind,
            Description = description,
            Tags = tags,
            Platforms = platforms,
            FilePath = Path.GetRelativePath(repoRoot, filePath),
            LineCount = lineCount,
            Triggers = triggers,
            References = references,
            ApproximateTokens = Math.Max(1, content.Length / 4)
        };
    }

    private static IEnumerable<string> EnumerateMarkdownFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (ShouldIgnore(directory))
                {
                    continue;
                }

                pending.Push(directory);
            }

            foreach (var filePath in Directory.EnumerateFiles(current, "*.md"))
            {
                yield return filePath;
            }
        }
    }

    private static bool MatchesCategory(CatalogItem item, string category)
    {
        return item.Tags.Any(tag => tag.Contains(category, StringComparison.OrdinalIgnoreCase))
               || item.Name.Contains(category, StringComparison.OrdinalIgnoreCase)
               || item.Description.Contains(category, StringComparison.OrdinalIgnoreCase);
    }

    private static CatalogSearchResult Score(CatalogItem item, IReadOnlyList<string> terms)
    {
        var score = 0;
        var reasons = new List<string>();
        var searchableId = Normalize(item.Id);
        var searchableName = Normalize(item.Name);
        var searchableDescription = Normalize(item.Description);
        var searchableTags = item.Tags.Select(Normalize).ToList();
        var searchableTriggers = item.Triggers.Select(Normalize).ToList();

        foreach (var term in terms)
        {
            if (searchableId.Equals(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
                reasons.Add($"exact id match: {term}");
                continue;
            }

            if (searchableName.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 40;
                reasons.Add($"name match: {term}");
            }

            if (searchableDescription.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 25;
                reasons.Add($"description match: {term}");
            }

            if (searchableTags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                score += 20;
                reasons.Add($"tag match: {term}");
            }

            if (searchableTriggers.Any(trigger => trigger.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                score += 15;
                reasons.Add($"trigger match: {term}");
            }
        }

        return new CatalogSearchResult
        {
            Item = item,
            Score = score,
            Reasons = reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static string? ExtractHeading(string content)
    {
        var match = HeadingRegex.Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static List<string> ExtractTriggers(string description, string content)
    {
        var combined = string.Concat(description, "\n", content);
        var match = TriggerRegex.Match(combined);
        if (!match.Success)
        {
            return new List<string>();
        }

        return match.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> SplitTerms(string query)
    {
        return query
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldIgnore(string directory)
    {
        return IgnoredDirectories.Any(ignored => directory.Contains(ignored, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
