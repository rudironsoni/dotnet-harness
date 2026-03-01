using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DotNetAgentHarness.Tools.Engine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DotNetAgentHarness.Tools;

public static class Program
{
    private static readonly string RulesyncDir = Path.Combine(Directory.GetCurrentDirectory(), ".rulesync");
    private static readonly string SkillsDir = Path.Combine(RulesyncDir, "skills");
    private static readonly string SubagentsDir = Path.Combine(RulesyncDir, "subagents");
    private static readonly string RulesDir = Path.Combine(RulesyncDir, "rules");
    private static readonly string CommandsDir = Path.Combine(RulesyncDir, "commands");

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

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/DotNetAgentHarness.Tools <lint-frontmatter|build-manifest>");
            return 1;
        }

        return args[0].ToLowerInvariant() switch
        {
            "lint-frontmatter" => RunLintFrontmatter(),
            "build-manifest" => RunBuildManifest(args.Skip(1).ToArray()),
            _ => 1
        };
    }

    private static int RunBuildManifest(string[] args)
    {
        var outputDir = Path.Combine(RulesyncDir, "manifest");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "skill-manifest.json");

        var manifest = ManifestBuilder.Build(SkillsDir);
        var json = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Manifest written to {outputPath}");
        return 0;
    }

    private static int RunLintFrontmatter()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var validSkills = GetDirectoryNames(SkillsDir);
        var validSubagents = GetSubagentNames(SubagentsDir);
        var validRefs = new HashSet<string>(validSkills.Concat(validSubagents));

        Console.WriteLine($"Found {validSkills.Count} skills in catalog");
        Console.WriteLine($"Found {validSubagents.Count} subagents in catalog\n");

        ValidateDirectory(SkillsDir, "skills", validRefs, validSubagents, errors, warnings, fileNameEndsWith: "SKILL.md");
        ValidateDirectory(SubagentsDir, "subagents", validRefs, validSubagents, errors, warnings, fileNameEndsWith: ".md");
        ValidateDirectory(RulesDir, "rules", validRefs, validSubagents, errors, warnings, fileNameEndsWith: ".md");
        ValidateDirectory(CommandsDir, "commands", validRefs, validSubagents, errors, warnings, fileNameEndsWith: ".md");

        Console.WriteLine(new string('=', 70));
        Console.WriteLine($"Errors: {errors.Count}");
        Console.WriteLine($"Warnings: {warnings.Count}");
        Console.WriteLine(new string('=', 70));

        if (errors.Count > 0)
        {
            Console.WriteLine("ERRORS:");
            foreach (var error in errors)
                Console.WriteLine($"  ❌ {error}");
        }

        if (warnings.Count > 0)
        {
            Console.WriteLine("WARNINGS:");
            foreach (var warning in warnings)
                Console.WriteLine($"  ⚠️  {warning}");
        }

        if (errors.Count == 0)
        {
            Console.WriteLine("✅ Frontmatter validation passed.");
            return 0;
        }

        Console.WriteLine("❌ Frontmatter validation failed.");
        return 1;
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
        if (!Directory.Exists(directory)) return;

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(fileNameEndsWith, StringComparison.OrdinalIgnoreCase))
                continue;

            ValidateFile(file, fileType, validRefs, validSubagents, errors, warnings);

            var fileName = Path.GetFileNameWithoutExtension(file);
            if ((fileType == "subagents" || fileType == "skills") && !IsKebabCase(fileName.Replace("SKILL", string.Empty, StringComparison.OrdinalIgnoreCase).Trim('-')))
            {
                warnings.Add($"{file}: Name should be kebab-case");
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
        var frontmatter = ExtractFrontmatter(content);

        if (frontmatter == null)
        {
            errors.Add($"{filePath}: Missing or invalid YAML frontmatter");
            return;
        }

        if (frontmatter.TryGetValue("_error", out var errorObj))
        {
            errors.Add($"{filePath}: YAML parsing error: {errorObj}");
            return;
        }

        if (RequiredFields.TryGetValue(fileType, out var required))
        {
            foreach (var field in required)
            {
                if (!frontmatter.ContainsKey(field))
                    errors.Add($"{filePath}: Missing required field '{field}'");
            }
        }

        foreach (var field in BannedTopLevelFields)
        {
            if (frontmatter.ContainsKey(field))
                errors.Add($"{filePath}: Banned field '{field}' at top level");
        }

        ValidateFieldOrder(frontmatter, fileType, filePath, warnings);

        if (fileType == "subagents")
            ValidateToolProfiles(frontmatter, filePath, errors);

        ValidateReferences(content, filePath, validRefs, validSubagents, errors);
    }

    private static Dictionary<string, object>? ExtractFrontmatter(string content)
    {
        var match = Regex.Match(content, @"^---\n([\s\S]*?)\n---", RegexOptions.Multiline);
        if (!match.Success) return null;

            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                return deserializer.Deserialize<Dictionary<string, object>>(match.Groups[1].Value);
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                // Restore graceful behavior: return a frontmatter-like dict with _error so callers can continue validation
                Console.Error.WriteLine($"YAML parsing error: {ex.Message}");
                return new Dictionary<string, object> { ["_error"] = ex.Message };
            }
            catch (Exception ex)
            {
                // Narrow catch: preserve original behavior but fail fast with diagnostic
                Console.Error.WriteLine($"Unexpected error parsing YAML frontmatter: {ex.Message}");
                // Convert TODO into issue placeholder: ISSUE-XXXX
                // See: https://github.com/<OWNER>/<REPO>/issues/ISSUE-XXXX
                throw;
            }
    }

    private static void ValidateFieldOrder(Dictionary<string, object> frontmatter, string fileType, string filePath, List<string> warnings)
    {
        if (!FieldOrder.TryGetValue(fileType, out var expectedOrder)) return;
        var actual = frontmatter.Keys.Where(k => !k.StartsWith('_')).ToList();

        for (var i = 0; i < expectedOrder.Length - 1; i++)
        {
            var current = expectedOrder[i];
            var next = expectedOrder[i + 1];

            var currentIdx = actual.IndexOf(current);
            var nextIdx = actual.IndexOf(next);
            if (currentIdx > nextIdx && nextIdx != -1)
                warnings.Add($"{filePath}: Field '{next}' should come before '{current}' (recommended order: {string.Join(", ", expectedOrder)})");
        }
    }

    private static void ValidateToolProfiles(Dictionary<string, object> frontmatter, string filePath, List<string> errors)
    {
        foreach (var platform in new[] { "claudecode", "opencode", "copilot" })
        {
            if (!frontmatter.TryGetValue(platform, out var platformObj) || platformObj is not Dictionary<object, object> platformMap)
                continue;

            if (platform == "claudecode" && platformMap.TryGetValue("allowed-tools", out var claudecodeTools) && claudecodeTools is IEnumerable<object> toolList)
            {
                foreach (var tool in toolList.Select(t => t.ToString()))
                {
                    if (!ValidTools["claudecode"].Contains(tool))
                        errors.Add($"{filePath}: Invalid tool '{tool}' in claudecode.allowed-tools");
                }
            }

            if ((platform == "opencode" || platform == "copilot") && platformMap.TryGetValue("tools", out var toolsObj))
            {
                if (platform == "opencode" && toolsObj is Dictionary<object, object> opencodeTools)
                {
                    foreach (var tool in opencodeTools.Keys.Select(k => k.ToString()))
                    {
                        if (!ValidTools["opencode"].Contains(tool))
                            errors.Add($"{filePath}: Invalid tool '{tool}' in opencode.tools");
                    }
                }

                if (platform == "copilot" && toolsObj is IEnumerable<object> copilotTools)
                {
                    foreach (var tool in copilotTools.Select(t => t.ToString()))
                    {
                        if (!ValidTools["copilot"].Contains(tool))
                            errors.Add($"{filePath}: Invalid tool '{tool}' in copilot.tools");
                    }
                }
            }
        }
    }

    private static void ValidateReferences(string content, string filePath, HashSet<string> validRefs, HashSet<string> validSubagents, List<string> errors)
    {
        foreach (Match match in SkillRefRegex.Matches(content))
        {
            var refName = match.Groups[1].Value;
            if (!validRefs.Contains(refName))
                errors.Add($"{filePath}: Invalid skill/subagent reference '{refName}'");
        }

        foreach (Match match in SubagentRefRegex.Matches(content))
        {
            var refName = match.Groups[1].Value;
            if (!validSubagents.Contains(refName))
                errors.Add($"{filePath}: Invalid subagent reference '{refName}'");
        }
    }

    private static HashSet<string> GetDirectoryNames(string directory)
    {
        if (!Directory.Exists(directory)) return new HashSet<string>();
        return new HashSet<string>(Directory.GetDirectories(directory).Select(Path.GetFileName)!.Where(n => !string.IsNullOrWhiteSpace(n))!);
    }

    private static HashSet<string> GetSubagentNames(string directory)
    {
        if (!Directory.Exists(directory)) return new HashSet<string>();
        return new HashSet<string>(Directory.GetFiles(directory, "*.md").Select(f => Path.GetFileNameWithoutExtension(f)));
    }

    private static bool IsKebabCase(string value) => Regex.IsMatch(value, "^[a-z0-9]+(?:-[a-z0-9]+)*$");
}
