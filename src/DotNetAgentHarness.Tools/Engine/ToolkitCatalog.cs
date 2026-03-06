using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNetAgentHarness.Tools.Engine;

public sealed class ToolkitCatalog
{
    public List<CatalogItem> Items { get; init; } = new();
    public CatalogStats Stats { get; init; } = new();

    public CatalogItem? Find(string id)
    {
        return Items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class CatalogItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
    public List<string> Platforms { get; init; } = new();
    public string FilePath { get; init; } = string.Empty;
    public int LineCount { get; init; }
    public List<string> Triggers { get; init; } = new();
    public List<string> References { get; init; } = new();
    public int ApproximateTokens { get; init; }
}

public sealed class CatalogStats
{
    public int TotalItems { get; init; }
    public int Skills { get; init; }
    public int Subagents { get; init; }
    public int Commands { get; init; }
    public int Personas { get; init; }
    public int TotalLines { get; init; }
}

public static class CatalogKinds
{
    public const string Skill = "skill";
    public const string Subagent = "subagent";
    public const string Command = "command";
    public const string Persona = "persona";
}

public sealed class CatalogSearchQuery
{
    public string Query { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? Platform { get; init; }
    public string? Kind { get; init; }
    public int Limit { get; init; } = 10;
}

public sealed class CatalogSearchResult
{
    public CatalogItem Item { get; init; } = new();
    public int Score { get; init; }
    public List<string> Reasons { get; init; } = new();
}

public sealed class CatalogComparison
{
    public CatalogItem Left { get; init; } = new();
    public CatalogItem Right { get; init; } = new();
    public List<string> SharedTags { get; init; } = new();
    public List<string> UniqueToLeft { get; init; } = new();
    public List<string> UniqueToRight { get; init; } = new();
}
