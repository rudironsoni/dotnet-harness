using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DotNetAgentHarness.Tools.Engine;

public static class MarkdownFrontmatter
{
    public static Dictionary<string, object> Parse(string content)
    {
        var trimmed = content.TrimStart('\uFEFF');
        var match = Regex.Match(trimmed, @"^---\n([\s\S]*?)\n---", RegexOptions.Multiline);
        if (!match.Success)
        {
            throw new InvalidDataException("Missing YAML frontmatter.");
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<Dictionary<string, object>>(match.Groups[1].Value)
               ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public static string? GetString(Dictionary<string, object> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    public static List<string> GetStringList(Dictionary<string, object> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return new List<string>();
        }

        if (value is IEnumerable<object> sequence)
        {
            var results = new List<string>();
            foreach (var item in sequence)
            {
                var text = item?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    results.Add(text);
                }
            }

            return results;
        }

        return string.IsNullOrWhiteSpace(value.ToString())
            ? new List<string>()
            : new List<string> { value.ToString()! };
    }

    public static List<string> DetectPlatforms(Dictionary<string, object> frontmatter)
    {
        var platforms = new List<string>();
        foreach (var platform in KnownPlatforms.All)
        {
            if (frontmatter.ContainsKey(platform))
            {
                platforms.Add(platform);
            }
        }

        return platforms.Count == 0 ? new List<string> { "*" } : platforms;
    }
}

public static class KnownPlatforms
{
    public static readonly string[] All = { "claudecode", "opencode", "copilot", "codexcli", "geminicli", "antigravity" };
}
