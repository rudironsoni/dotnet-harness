using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DotNetAgentHarness.Tools.Engine;

public static class GraphEngine
{
    public static GraphReport Build(string repoRoot, GraphOptions options)
    {
        var catalog = ToolkitCatalogLoader.Load(repoRoot);
        var nodes = ResolveNodes(catalog, options);
        var nodeIds = nodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edges = BuildEdges(catalog, nodes, nodeIds);
        var rendered = Render(options.Format, nodes, edges);

        return new GraphReport
        {
            RootId = options.ItemId ?? string.Empty,
            Category = options.Category ?? string.Empty,
            Kind = options.Kind ?? CatalogKinds.Skill,
            Depth = Math.Max(1, options.Depth),
            Format = options.Format,
            Nodes = nodes
                .OrderBy(node => node.Kind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Edges = edges
                .OrderBy(edge => edge.FromId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.ToId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Hubs = ResolveHubs(nodes, edges),
            Orphans = ResolveOrphans(nodes, edges),
            RenderedGraph = rendered
        };
    }

    private static List<GraphNode> ResolveNodes(ToolkitCatalog catalog, GraphOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ItemId))
        {
            return ResolveReachableNodes(catalog, options.ItemId, Math.Max(1, options.Depth));
        }

        var kind = string.IsNullOrWhiteSpace(options.Kind) ? CatalogKinds.Skill : options.Kind;
        return catalog.Items
            .Where(item => item.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
            .Where(item => MatchesCategory(item, options.Category))
            .Select(ToNode)
            .ToList();
    }

    private static List<GraphNode> ResolveReachableNodes(ToolkitCatalog catalog, string itemId, int depth)
    {
        var root = catalog.Find(itemId) ?? throw new ArgumentException($"Catalog item '{itemId}' was not found.");
        var visited = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase)
        {
            [root.Id] = ToNode(root)
        };
        var queue = new Queue<(CatalogItem Item, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Depth >= depth)
            {
                continue;
            }

            foreach (var referenceId in current.Item.References)
            {
                var referenced = catalog.Find(referenceId);
                if (referenced is null)
                {
                    continue;
                }

                if (!visited.ContainsKey(referenced.Id))
                {
                    visited[referenced.Id] = ToNode(referenced);
                    queue.Enqueue((referenced, current.Depth + 1));
                }
            }
        }

        return visited.Values.ToList();
    }

    private static List<GraphEdge> BuildEdges(ToolkitCatalog catalog, IReadOnlyList<GraphNode> nodes, IReadOnlySet<string> nodeIds)
    {
        var edges = new List<GraphEdge>();
        foreach (var node in nodes)
        {
            var item = catalog.Find(node.Id);
            if (item is null)
            {
                continue;
            }

            foreach (var referenceId in item.References.Where(nodeIds.Contains))
            {
                edges.Add(new GraphEdge
                {
                    FromId = item.Id,
                    ToId = referenceId
                });
            }
        }

        return edges;
    }

    private static List<GraphHub> ResolveHubs(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        return nodes
            .Select(node =>
            {
                var incoming = edges.Count(edge => edge.ToId.Equals(node.Id, StringComparison.OrdinalIgnoreCase));
                var outgoing = edges.Count(edge => edge.FromId.Equals(node.Id, StringComparison.OrdinalIgnoreCase));
                return new GraphHub
                {
                    Id = node.Id,
                    Kind = node.Kind,
                    Degree = incoming + outgoing,
                    Incoming = incoming,
                    Outgoing = outgoing
                };
            })
            .Where(hub => hub.Degree > 1)
            .OrderByDescending(hub => hub.Degree)
            .ThenBy(hub => hub.Id, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static List<string> ResolveOrphans(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        return nodes
            .Where(node => !edges.Any(edge =>
                edge.FromId.Equals(node.Id, StringComparison.OrdinalIgnoreCase)
                || edge.ToId.Equals(node.Id, StringComparison.OrdinalIgnoreCase)))
            .Select(node => node.Id)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static GraphNode ToNode(CatalogItem item)
    {
        return new GraphNode
        {
            Id = item.Id,
            Label = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
            Kind = item.Kind
        };
    }

    private static bool MatchesCategory(CatalogItem item, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return true;
        }

        return item.Tags.Any(tag => tag.Contains(category, StringComparison.OrdinalIgnoreCase))
               || item.Description.Contains(category, StringComparison.OrdinalIgnoreCase)
               || item.Name.Contains(category, StringComparison.OrdinalIgnoreCase);
    }

    private static string Render(string format, IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        var normalized = (format ?? "mermaid").Trim().ToLowerInvariant();
        return normalized switch
        {
            "mermaid" => RenderMermaid(nodes, edges),
            "dot" => RenderDot(nodes, edges),
            _ => throw new ArgumentException($"Unsupported graph format '{format}'. Supported values: mermaid, dot, json.")
        };
    }

    private static string RenderMermaid(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        var builder = new StringBuilder();
        builder.AppendLine("graph TD");
        foreach (var node in nodes)
        {
            builder.AppendLine($"    {SanitizeId(node.Id)}[\"{EscapeLabel(node.Label)}\\n{node.Kind}\"]");
        }

        foreach (var edge in edges)
        {
            builder.AppendLine($"    {SanitizeId(edge.FromId)} --> {SanitizeId(edge.ToId)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderDot(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        var builder = new StringBuilder();
        builder.AppendLine("digraph dotnet_agent_harness {");
        builder.AppendLine("  rankdir=LR;");
        foreach (var node in nodes)
        {
            builder.AppendLine($"  \"{node.Id}\" [label=\"{EscapeLabel(node.Label)}\\n{node.Kind}\"];");
        }

        foreach (var edge in edges)
        {
            builder.AppendLine($"  \"{edge.FromId}\" -> \"{edge.ToId}\";");
        }

        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private static string SanitizeId(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return chars.Length == 0 ? "node" : new string(chars);
    }

    private static string EscapeLabel(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

public sealed class GraphOptions
{
    public string? ItemId { get; init; }
    public string? Category { get; init; }
    public string? Kind { get; init; } = CatalogKinds.Skill;
    public int Depth { get; init; } = 3;
    public string Format { get; init; } = "mermaid";
}

public sealed class GraphNode
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
}

public sealed class GraphEdge
{
    public string FromId { get; init; } = string.Empty;
    public string ToId { get; init; } = string.Empty;
}

public sealed class GraphHub
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public int Degree { get; init; }
    public int Incoming { get; init; }
    public int Outgoing { get; init; }
}

public sealed class GraphReport
{
    public string RootId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public int Depth { get; init; }
    public string Format { get; init; } = string.Empty;
    public List<GraphNode> Nodes { get; init; } = new();
    public List<GraphEdge> Edges { get; init; } = new();
    public List<GraphHub> Hubs { get; init; } = new();
    public List<string> Orphans { get; init; } = new();
    public string RenderedGraph { get; init; } = string.Empty;
}
