using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DotNetAgentHarness.Tools.Engine;

public static class FrontmatterLinter
{
    private static readonly Dictionary<string, string[]> FieldOrder = new()
    {
        ["skills"] = new[] { "name", "description", "targets", "tags", "version", "author" },
        ["subagents"] = new[] { "name", "description", "targets", "tags", "version", "author" },
        ["rules"] = new[] { "root", "localRoot", "targets", "description", "globs" },
        ["commands"] = new[] { "description", "targets" },
    };

    private static readonly Dictionary<string, string[]> RequiredFields = new()
    {
        ["skills"] = new[] { "name", "description", "targets" },
        ["subagents"] = new[] { "name", "description", "targets" },
        ["rules"] = new[] { "targets", "description" },
        ["commands"] = new[] { "description", "targets" },
    };

    private static readonly string[] BannedTopLevelFields = { "tools", "model", "mode" };

    private static readonly Dictionary<string, string[]> ValidTools = new()
    {
        ["claudecode"] = new[] { "Read", "Grep", "Glob", "Bash", "Edit", "Write" },
        ["opencode"] = new[] { "bash", "edit", "write" },
        ["copilot"] = new[] { "read", "search", "execute", "edit" },
    };

    private static readonly Regex SkillRefRegex = new(@"\[skill:([^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex SubagentRefRegex = new(@"\[subagent:([^\]]+)\]", RegexOptions.Compiled);

    public static LintResult Lint(string repoRoot)
    {
        var rulesyncDir = Path.Combine(repoRoot, ".rulesync");
        var skillsDir = Path.Combine(rulesyncDir, "skills");
        var subagentsDir = Path.Combine(rulesyncDir, "subagents");
        var rulesDir = Path.Combine(rulesyncDir, "rules");
        var commandsDir = Path.Combine(rulesyncDir, "commands");

        var errors = new List<string>();
        var warnings = new List<string>();

        var validSkills = GetDirectoryNames(skillsDir);
        var validSubagents = GetSubagentNames(subagentsDir);
        var validRefs = new HashSet<string>(validSkills.Concat(validSubagents), StringComparer.OrdinalIgnoreCase);

        ValidateDirectory(skillsDir, "skills", validRefs, validSubagents, errors, warnings, "SKILL.md");
        ValidateDirectory(subagentsDir, "subagents", validRefs, validSubagents, errors, warnings, ".md");
        ValidateDirectory(rulesDir, "rules", validRefs, validSubagents, errors, warnings, ".md");
        ValidateDirectory(commandsDir, "commands", validRefs, validSubagents, errors, warnings, ".md");

        return new LintResult
        {
            Errors = errors,
            Warnings = warnings
        };
    }

    private static void ValidateDirectory(
        string directory,
        string fileType,
        HashSet<string> validRefs,
        HashSet<string> validSubagents,
        List<string> errors,
        List<string> warnings,
        string fileNameEndsWith)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(fileNameEndsWith, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ValidateFile(file, fileType, validRefs, validSubagents, errors, warnings);

            var fileName = Path.GetFileNameWithoutExtension(file);
            if ((fileType == "subagents" || fileType == "skills")
                && !IsKebabCase(fileName.Replace("SKILL", string.Empty, StringComparison.OrdinalIgnoreCase).Trim('-')))
            {
                warnings.Add($"{file}: Name should be kebab-case.");
            }
        }
    }

    private static void ValidateFile(
        string filePath,
        string fileType,
        HashSet<string> validRefs,
        HashSet<string> validSubagents,
        List<string> errors,
        List<string> warnings)
    {
        var content = File.ReadAllText(filePath);
        Dictionary<string, object> frontmatter;

        try
        {
            frontmatter = MarkdownFrontmatter.Parse(content);
        }
        catch (Exception ex)
        {
            errors.Add($"{filePath}: {ex.Message}");
            return;
        }

        if (RequiredFields.TryGetValue(fileType, out var required))
        {
            foreach (var field in required.Where(field => !frontmatter.ContainsKey(field)))
            {
                errors.Add($"{filePath}: Missing required field '{field}'.");
            }
        }

        foreach (var field in BannedTopLevelFields.Where(frontmatter.ContainsKey))
        {
            errors.Add($"{filePath}: Banned field '{field}' at top level.");
        }

        ValidateFieldOrder(frontmatter, fileType, filePath, warnings);

        if (fileType == "subagents")
        {
            ValidateToolProfiles(frontmatter, filePath, errors);
        }

        ValidateReferences(content, filePath, validRefs, validSubagents, errors);
    }

    private static void ValidateFieldOrder(Dictionary<string, object> frontmatter, string fileType, string filePath, List<string> warnings)
    {
        if (!FieldOrder.TryGetValue(fileType, out var expectedOrder))
        {
            return;
        }

        var actual = frontmatter.Keys.ToList();
        for (var i = 0; i < expectedOrder.Length - 1; i++)
        {
            var current = expectedOrder[i];
            var next = expectedOrder[i + 1];
            var currentIndex = actual.IndexOf(current);
            var nextIndex = actual.IndexOf(next);

            if (currentIndex > nextIndex && nextIndex != -1)
            {
                warnings.Add($"{filePath}: Field '{next}' should come before '{current}'.");
            }
        }
    }

    private static void ValidateToolProfiles(Dictionary<string, object> frontmatter, string filePath, List<string> errors)
    {
        foreach (var platform in new[] { "claudecode", "opencode", "copilot" })
        {
            if (!frontmatter.TryGetValue(platform, out var platformObj) || platformObj is not Dictionary<object, object> platformMap)
            {
                continue;
            }

            if (platform == "claudecode"
                && platformMap.TryGetValue("allowed-tools", out var allowedTools)
                && allowedTools is IEnumerable<object> tools)
            {
                foreach (var tool in tools.Select(item => item.ToString()))
                {
                    if (!ValidTools["claudecode"].Contains(tool))
                    {
                        errors.Add($"{filePath}: Invalid tool '{tool}' in claudecode.allowed-tools.");
                    }
                }
            }

            if (platform == "opencode"
                && platformMap.TryGetValue("tools", out var opencodeTools)
                && opencodeTools is Dictionary<object, object> opencodeMap)
            {
                foreach (var tool in opencodeMap.Keys.Select(key => key.ToString()))
                {
                    if (!ValidTools["opencode"].Contains(tool))
                    {
                        errors.Add($"{filePath}: Invalid tool '{tool}' in opencode.tools.");
                    }
                }
            }

            if (platform == "copilot"
                && platformMap.TryGetValue("tools", out var copilotTools)
                && copilotTools is IEnumerable<object> copilotList)
            {
                foreach (var tool in copilotList.Select(item => item.ToString()))
                {
                    if (!ValidTools["copilot"].Contains(tool))
                    {
                        errors.Add($"{filePath}: Invalid tool '{tool}' in copilot.tools.");
                    }
                }
            }
        }
    }

    private static void ValidateReferences(string content, string filePath, HashSet<string> validRefs, HashSet<string> validSubagents, List<string> errors)
    {
        foreach (Match match in SkillRefRegex.Matches(content))
        {
            var reference = match.Groups[1].Value;
            if (!validRefs.Contains(reference))
            {
                errors.Add($"{filePath}: Invalid skill/subagent reference '{reference}'.");
            }
        }

        foreach (Match match in SubagentRefRegex.Matches(content))
        {
            var reference = match.Groups[1].Value;
            if (!validSubagents.Contains(reference))
            {
                errors.Add($"{filePath}: Invalid subagent reference '{reference}'.");
            }
        }
    }

    private static HashSet<string> GetDirectoryNames(string directory)
    {
        return !Directory.Exists(directory)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : Directory.GetDirectories(directory)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    private static HashSet<string> GetSubagentNames(string directory)
    {
        return !Directory.Exists(directory)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : Directory.GetFiles(directory, "*.md")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    private static bool IsKebabCase(string value)
    {
        return Regex.IsMatch(value, "^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }
}

public sealed class LintResult
{
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public bool Passed => Errors.Count == 0;
}
