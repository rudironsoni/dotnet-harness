using System;
using System.Collections.Generic;

namespace DotNetAgentHarness.Tools.Engine;

public sealed class CliArguments
{
    private readonly Dictionary<string, string?> options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> positionals = new();

    public CliArguments(IEnumerable<string> args)
    {
        var tokens = new List<string>(args);
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(token);
                continue;
            }

            var separatorIndex = token.IndexOf('=');
            if (separatorIndex >= 0)
            {
                var key = token[..separatorIndex];
                var value = token[(separatorIndex + 1)..];
                options[key] = value;
                continue;
            }

            if (i + 1 < tokens.Count && !tokens[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[token] = tokens[i + 1];
                i++;
                continue;
            }

            options[token] = "true";
        }
    }

    public IReadOnlyList<string> Positionals => positionals;

    public bool HasFlag(string name)
    {
        if (!options.TryGetValue(name, out var value))
        {
            return false;
        }

        return value is null
               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public string? GetOption(string name)
    {
        return options.TryGetValue(name, out var value) ? value : null;
    }

    public int GetIntOption(string name, int defaultValue)
    {
        var raw = GetOption(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, out var value))
        {
            throw new ArgumentException($"Option {name} must be an integer. Received '{raw}'.");
        }

        return value;
    }
}
