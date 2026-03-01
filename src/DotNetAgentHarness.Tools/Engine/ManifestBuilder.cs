using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DotNetAgentHarness.Tools.Engine;

public static class ManifestBuilder
{
    private static readonly Regex SkillRefRegex = new(@"\[skill:([a-z0-9-]+)\]", RegexOptions.Compiled);

    public static SkillManifest Build(string skillsDir)
    {
        if (!Directory.Exists(skillsDir))
            throw new DirectoryNotFoundException($"Skills directory not found: {skillsDir}");

        var manifest = new SkillManifest
        {
            Version = "1.0.0",
            GeneratedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };

        var skillFolders = Directory.GetDirectories(skillsDir);
        var skillNames = skillFolders.Select(Path.GetFileName).Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var skillDir in skillFolders)
        {
            var folderName = Path.GetFileName(skillDir) ?? string.Empty;
            var skillFile = Path.Combine(skillDir, "SKILL.md");

            if (!File.Exists(skillFile))
            {
                manifest.Errors.Add(new ManifestError(folderName, "Missing SKILL.md"));
                continue;
            }

            try
            {
                var content = File.ReadAllText(skillFile);
                var frontmatter = ParseFrontmatter(content);
                var referencedSkills = SkillRefRegex.Matches(content)
                    .Select(m => m.Groups[1].Value)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var skill = BuildSkillEntry(folderName, skillFile, content, frontmatter, referencedSkills);
                manifest.Skills[folderName] = skill;
            }
            catch (Exception ex)
            {
                // Log the error context and rethrow to avoid silent swallowing in higher-level flows
                Console.Error.WriteLine($"Unhandled exception in ManifestBuilder.cs: {ex}");
                throw;
            }
        }

        ComputeStats(manifest);
        ComputeCircularDependencies(manifest);
        ComputeConflicts(manifest, skillNames);
        ComputeInferredDependencies(manifest);

        return manifest;
    }

    private static SkillEntry BuildSkillEntry(
        string folderName,
        string skillFile,
        string content,
        Dictionary<string, object> frontmatter,
        List<string> referencedSkills)
    {
        var name = GetString(frontmatter, "name") ?? folderName;
        var description = GetString(frontmatter, "description") ?? string.Empty;
        var version = GetString(frontmatter, "version") ?? "0.0.1";
        var tags = GetStringList(frontmatter, "tags");
        var dependsOn = GetStringList(frontmatter, "depends_on");
        var optional = GetStringList(frontmatter, "optional");
        var conflicts = GetStringList(frontmatter, "conflicts_with");

        var platforms = DetectPlatforms(frontmatter);
        var lineCount = content.Split('\n').Length;

        return new SkillEntry
        {
            Name = name,
            Description = description,
            Version = version,
            Tags = tags,
            DependsOn = dependsOn,
            Optional = optional,
            ConflictsWith = conflicts,
            FilePath = skillFile.Replace(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, string.Empty),
            LineCount = lineCount,
            Platforms = platforms,
            ReferencedSkills = referencedSkills
        };
    }

    private static Dictionary<string, object> ParseFrontmatter(string content)
    {
        var match = Regex.Match(content, @"^---\n([\s\S]*?)\n---", RegexOptions.Multiline);
        if (!match.Success)
            throw new InvalidDataException("Missing YAML frontmatter");

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<Dictionary<string, object>>(match.Groups[1].Value)
               ?? new Dictionary<string, object>();
    }

    private static List<string> DetectPlatforms(Dictionary<string, object> frontmatter)
    {
        var platforms = new List<string>();
        var known = new[] { "claudecode", "opencode", "copilot", "codexcli", "geminicli" };
        foreach (var platform in known)
        {
            if (frontmatter.ContainsKey(platform))
                platforms.Add(platform);
        }

        return platforms.Count == 0 ? new List<string> { "*" } : platforms;
    }

    private static void ComputeStats(SkillManifest manifest)
    {
        manifest.Stats.TotalSkills = manifest.Skills.Count;
        manifest.Stats.WithDependencies = manifest.Skills.Values.Count(s => s.DependsOn.Count > 0);
        manifest.Stats.WithConflicts = manifest.Skills.Values.Count(s => s.ConflictsWith.Count > 0);
        manifest.Stats.Errors = manifest.Errors.Count;
    }

    private static void ComputeCircularDependencies(SkillManifest manifest)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var skill in manifest.Skills.Keys)
        {
            if (DetectCycle(skill, manifest, visited, stack, out var cycle))
            {
                manifest.CircularDependencies.Add(cycle);
            }
        }

        manifest.Stats.CircularDependencies = manifest.CircularDependencies.Count;
    }

    private static bool DetectCycle(
        string skill,
        SkillManifest manifest,
        HashSet<string> visited,
        HashSet<string> stack,
        out CircularDependency cycle)
    {
        cycle = new CircularDependency();

        if (stack.Contains(skill))
        {
            cycle = new CircularDependency { Skills = new List<string>(stack) { skill } };
            return true;
        }

        if (visited.Contains(skill))
            return false;

        visited.Add(skill);
        stack.Add(skill);

        if (manifest.Skills.TryGetValue(skill, out var entry))
        {
            foreach (var dep in entry.DependsOn)
            {
                if (DetectCycle(dep, manifest, visited, stack, out cycle))
                    return true;
            }
        }

        stack.Remove(skill);
        return false;
    }

    private static void ComputeConflicts(SkillManifest manifest, HashSet<string> knownSkills)
    {
        foreach (var (skillName, entry) in manifest.Skills)
        {
            foreach (var conflict in entry.ConflictsWith)
            {
                if (!knownSkills.Contains(conflict))
                {
                    manifest.VersionConflicts.Add(new ConflictEntry
                    {
                        Skill = skillName,
                        Type = "missing_conflict_target",
                        Conflicts = new List<string> { conflict }
                    });
                    continue;
                }

                if (manifest.Skills.TryGetValue(conflict, out var other) && !other.ConflictsWith.Contains(skillName, StringComparer.OrdinalIgnoreCase))
                {
                    manifest.VersionConflicts.Add(new ConflictEntry
                    {
                        Skill = skillName,
                        Type = "asymmetric_conflict",
                        Conflicts = new List<string> { conflict },
                        Message = $"{skillName} conflicts with {conflict} but not vice versa"
                    });
                }
            }
        }

        manifest.Stats.VersionConflicts = manifest.VersionConflicts.Count;
    }

    private static void ComputeInferredDependencies(SkillManifest manifest)
    {
        foreach (var entry in manifest.Skills.Values)
        {
            var inferred = entry.ReferencedSkills
                .Where(s => !s.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
                .Where(s => !entry.DependsOn.Contains(s, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            entry.InferredDependencies = inferred;
        }
    }

    private static string? GetString(Dictionary<string, object> dict, string key)
    {
        return dict.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static List<string> GetStringList(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value is null) return new List<string>();
        if (value is IEnumerable<object> list)
            return list.Select(v => v.ToString() ?? string.Empty).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        return new List<string>();
    }
}

public class SkillManifest
{
    public string Version { get; set; } = "1.0.0";
    public string GeneratedAt { get; set; } = string.Empty;
    public Dictionary<string, SkillEntry> Skills { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ManifestStats Stats { get; set; } = new();
    public List<ManifestError> Errors { get; set; } = new();
    public List<CircularDependency> CircularDependencies { get; set; } = new();
    public List<ConflictEntry> VersionConflicts { get; set; } = new();
}

public class SkillEntry
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "0.0.1";
    public List<string> Tags { get; set; } = new();
    public List<string> DependsOn { get; set; } = new();
    public List<string> Optional { get; set; } = new();
    public List<string> ConflictsWith { get; set; } = new();
    public List<string> InferredDependencies { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public List<string> Platforms { get; set; } = new();
    public List<string> ReferencedSkills { get; set; } = new();
}

public class ManifestStats
{
    public int TotalSkills { get; set; }
    public int WithDependencies { get; set; }
    public int WithConflicts { get; set; }
    public int Errors { get; set; }
    public int CircularDependencies { get; set; }
    public int VersionConflicts { get; set; }
}

public class ManifestError
{
    public string Skill { get; set; }
    public string Error { get; set; }

    public ManifestError(string skill, string error)
    {
        Skill = skill;
        Error = error;
    }
}

public class CircularDependency
{
    public List<string> Skills { get; set; } = new();
}

public class ConflictEntry
{
    public string Skill { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> Conflicts { get; set; } = new();
    public string? Message { get; set; }
}
