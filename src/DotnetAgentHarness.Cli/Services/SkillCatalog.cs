namespace DotnetAgentHarness.Cli.Services;

using DotnetAgentHarness.Cli.Models;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using YamlDotNet.RepresentationModel;

/// <summary>
/// Implementation of ISkillCatalog that parses YAML frontmatter from markdown files.
/// </summary>
public sealed class SkillCatalog : ISkillCatalog
{
    private readonly string basePath;
    private readonly string rulesyncPath;

    /// <inheritdoc />
    public string BasePath => this.basePath;

    /// <summary>
    /// Creates a new SkillCatalog instance.
    /// </summary>
    public SkillCatalog(string? basePath = null)
    {
        this.basePath = basePath ?? Directory.GetCurrentDirectory();
        this.rulesyncPath = Path.Combine(this.basePath, ".rulesync");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillInfo>> GetSkillsAsync(CancellationToken ct = default)
    {
        var skills = new List<SkillInfo>();
        string skillsPath = Path.Combine(this.rulesyncPath, "skills");

        if (!Directory.Exists(skillsPath))
        {
            return skills;
        }

        foreach (string skillFile in Directory.GetFiles(skillsPath, "SKILL.md", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            SkillInfo? skill = await ParseSkillFileAsync(skillFile, ct);
            if (skill != null)
            {
                skills.Add(skill);
            }
        }

        return skills;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubagentInfo>> GetSubagentsAsync(CancellationToken ct = default)
    {
        var subagents = new List<SubagentInfo>();
        string subagentsPath = Path.Combine(this.rulesyncPath, "subagents");

        if (!Directory.Exists(subagentsPath))
        {
            return subagents;
        }

        foreach (string subagentFile in Directory.GetFiles(subagentsPath, "*.md", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            SubagentInfo? subagent = await ParseSubagentFileAsync(subagentFile, ct);
            if (subagent != null)
            {
                subagents.Add(subagent);
            }
        }

        return subagents;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommandInfo>> GetCommandsAsync(CancellationToken ct = default)
    {
        var commands = new List<CommandInfo>();
        string commandsPath = Path.Combine(this.rulesyncPath, "commands");

        if (!Directory.Exists(commandsPath))
        {
            return commands;
        }

        foreach (string commandFile in Directory.GetFiles(commandsPath, "*.md", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            CommandInfo? command = await ParseCommandFileAsync(commandFile, ct);
            if (command != null)
            {
                commands.Add(command);
            }
        }

        return commands;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillInfo>> SearchSkillsAsync(
        string? query = null,
        string? category = null,
        string? subcategory = null,
        string? complexity = null,
        string? platform = null,
        int limit = 10,
        CancellationToken ct = default)
    {
        var skills = await this.GetSkillsAsync(ct);
        var results = skills.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            string normalizedQuery = query.ToLowerInvariant();
            results = results.Where(s =>
                s.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                (s.Description?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                s.Tags.Any(t => t.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            results = results.Where(s =>
                s.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrWhiteSpace(subcategory))
        {
            results = results.Where(s =>
                s.Subcategory?.Equals(subcategory, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrWhiteSpace(complexity))
        {
            results = results.Where(s =>
                s.Complexity?.Equals(complexity, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            results = results.Where(s =>
                s.Targets.Contains("*") ||
                s.Targets.Any(t => t.Equals(platform, StringComparison.OrdinalIgnoreCase)));
        }

        return results.Take(limit).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubagentInfo>> SearchSubagentsAsync(
        string? query = null,
        string? platform = null,
        int limit = 10,
        CancellationToken ct = default)
    {
        var subagents = await this.GetSubagentsAsync(ct);
        var results = subagents.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            string normalizedQuery = query.ToLowerInvariant();
            results = results.Where(s =>
                s.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                (s.Description?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                s.Tags.Any(t => t.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            results = results.Where(s =>
                s.Targets.Contains("*") ||
                s.Targets.Any(t => t.Equals(platform, StringComparison.OrdinalIgnoreCase)));
        }

        return results.Take(limit).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommandInfo>> SearchCommandsAsync(
        string? query = null,
        string? platform = null,
        int limit = 10,
        CancellationToken ct = default)
    {
        var commands = await this.GetCommandsAsync(ct);
        var results = commands.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            string normalizedQuery = query.ToLowerInvariant();
            results = results.Where(c =>
                c.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                (c.Description?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                c.Tags.Any(t => t.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            results = results.Where(c =>
                c.Targets.Contains("*") ||
                c.Targets.Any(t => t.Equals(platform, StringComparison.OrdinalIgnoreCase)));
        }

        return results.Take(limit).ToList();
    }

    /// <inheritdoc />
    public async Task<SkillInfo?> GetSkillByNameAsync(string name, CancellationToken ct = default)
    {
        var skills = await this.GetSkillsAsync(ct);
        return skills.FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<SubagentInfo?> GetSubagentByNameAsync(string name, CancellationToken ct = default)
    {
        var subagents = await this.GetSubagentsAsync(ct);
        return subagents.FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<CommandInfo?> GetCommandByNameAsync(string name, CancellationToken ct = default)
    {
        var commands = await this.GetCommandsAsync(ct);
        return commands.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<CatalogStats> GetStatsAsync(CancellationToken ct = default)
    {
        var skills = await this.GetSkillsAsync(ct);
        var subagents = await this.GetSubagentsAsync(ct);
        var commands = await this.GetCommandsAsync(ct);

        var skillsByCategory = skills
            .Where(s => !string.IsNullOrEmpty(s.Category))
            .GroupBy(s => s.Category!)
            .ToDictionary(g => g.Key, g => g.Count());

        var skillsByComplexity = skills
            .Where(s => !string.IsNullOrEmpty(s.Complexity))
            .GroupBy(s => s.Complexity!)
            .ToDictionary(g => g.Key, g => g.Count());

        var allTags = skills.SelectMany(s => s.Tags).ToList();
        var topTags = allTags
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count())
            .OrderByDescending(kvp => kvp.Value)
            .Take(20)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        int totalLines = skills.Sum(s => s.LineCount) +
                         subagents.Sum(s => s.LineCount) +
                         commands.Sum(c => c.LineCount);

        return new CatalogStats
        {
            TotalSkills = skills.Count,
            TotalSubagents = subagents.Count,
            TotalCommands = commands.Count,
            TotalLines = totalLines,
            SkillsByCategory = skillsByCategory,
            SkillsByComplexity = skillsByComplexity,
            TotalTags = allTags.Distinct().Count(),
            TopTags = topTags,
        };
    }

    private static async Task<SkillInfo?> ParseSkillFileAsync(string filePath, CancellationToken ct)
    {
        try
        {
            string content = await File.ReadAllTextAsync(filePath, ct);
            var frontmatter = ParseFrontmatter(content);

            if (frontmatter?.ContainsKey("name") != true)
            {
                return null;
            }

            var directoryName = Path.GetFileName(Path.GetDirectoryName(filePath));

            return new SkillInfo
            {
                Name = GetString(frontmatter, "name") ?? directoryName ?? "unknown",
                Title = GetString(frontmatter, "title"),
                Category = GetString(frontmatter, "category"),
                Subcategory = GetString(frontmatter, "subcategory"),
                Description = GetString(frontmatter, "description"),
                Tags = GetStringList(frontmatter, "tags"),
                Targets = GetStringList(frontmatter, "targets"),
                Version = GetString(frontmatter, "version"),
                Author = GetString(frontmatter, "author"),
                Invocable = GetBool(frontmatter, "invocable"),
                Complexity = GetString(frontmatter, "complexity"),
                RelatedSkills = GetStringList(frontmatter, "related_skills"),
                FilePath = filePath,
                DirectoryPath = Path.GetDirectoryName(filePath),
                LineCount = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).Length,
                PlatformConfig = GetPlatformBlocks(frontmatter),
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<SubagentInfo?> ParseSubagentFileAsync(string filePath, CancellationToken ct)
    {
        try
        {
            string content = await File.ReadAllTextAsync(filePath, ct);
            var frontmatter = ParseFrontmatter(content);

            if (frontmatter?.ContainsKey("name") != true)
            {
                return null;
            }

            var fileName = Path.GetFileNameWithoutExtension(filePath);

            return new SubagentInfo
            {
                Name = GetString(frontmatter, "name") ?? fileName,
                Description = GetString(frontmatter, "description"),
                Targets = GetStringList(frontmatter, "targets"),
                Version = GetString(frontmatter, "version"),
                Author = GetString(frontmatter, "author"),
                Role = GetString(frontmatter, "role"),
                Tags = GetStringList(frontmatter, "tags"),
                FilePath = filePath,
                LineCount = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).Length,
                PlatformConfig = GetPlatformBlocks(frontmatter),
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<CommandInfo?> ParseCommandFileAsync(string filePath, CancellationToken ct)
    {
        try
        {
            string content = await File.ReadAllTextAsync(filePath, ct);
            var frontmatter = ParseFrontmatter(content);

            if (frontmatter?.ContainsKey("name") != true)
            {
                return null;
            }

            var fileName = Path.GetFileNameWithoutExtension(filePath);

            return new CommandInfo
            {
                Name = GetString(frontmatter, "name") ?? fileName,
                Description = GetString(frontmatter, "description"),
                Targets = GetStringList(frontmatter, "targets"),
                Portability = GetString(frontmatter, "portability"),
                FlatteningRisk = GetString(frontmatter, "flattening-risk"),
                Simulated = GetBool(frontmatter, "simulated"),
                Version = GetString(frontmatter, "version"),
                Author = GetString(frontmatter, "author"),
                Tags = GetStringList(frontmatter, "tags"),
                FilePath = filePath,
                LineCount = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).Length,
                PlatformConfig = GetPlatformBlocks(frontmatter),
            };
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, object>? ParseFrontmatter(string content)
    {
        // Use Markdig to parse YAML frontmatter
        MarkdownDocument document = Markdown.Parse(content);
        YamlFrontMatterBlock? yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();

        if (yamlBlock == null)
        {
            return null;
        }

        // Extract YAML content from the lines
        string yamlContent = string.Join("\n", yamlBlock.Lines);
        var yaml = new YamlStream();
        yaml.Load(new StringReader(yamlContent));

        if (yaml.Documents.Count == 0)
        {
            return null;
        }

        if (yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            return null;
        }

        var result = new Dictionary<string, object>();
        foreach (var entry in root.Children)
        {
            string key = entry.Key.ToString();
            result[key] = ConvertYamlNode(entry.Value);
        }

        return result;
    }

    private static object ConvertYamlNode(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => scalar.Value ?? string.Empty,
            YamlSequenceNode sequence => sequence.Children.Select(ConvertYamlNode).ToList(),
            YamlMappingNode mapping => mapping.Children.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => ConvertYamlNode(kvp.Value)),
            _ => string.Empty,
        };
    }

    private static string? GetString(Dictionary<string, object> dict, string key)
    {
        return dict.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static IReadOnlyList<string> GetStringList(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value))
        {
            return Array.Empty<string>();
        }

        return value switch
        {
            List<object> list => list.Select(i => i?.ToString() ?? string.Empty).Where(s => !string.IsNullOrEmpty(s)).ToList(),
            string str => !string.IsNullOrEmpty(str) ? [str] : Array.Empty<string>(),
            _ => Array.Empty<string>(),
        };
    }

    private static bool GetBool(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value))
        {
            return false;
        }

        return value is string str && bool.TryParse(str, out bool result) && result;
    }

    private static Dictionary<string, Dictionary<string, object>> GetPlatformBlocks(Dictionary<string, object> frontmatter)
    {
        string[] platforms = ["claudecode", "opencode", "copilot", "codexcli", "geminicli", "antigravity", "factorydroid"];
        var result = new Dictionary<string, Dictionary<string, object>>();

        foreach (string platform in platforms)
        {
            if (frontmatter.TryGetValue(platform, out var value) && value is Dictionary<string, object> dict)
            {
                result[platform] = dict;
            }
        }

        return result;
    }
}
