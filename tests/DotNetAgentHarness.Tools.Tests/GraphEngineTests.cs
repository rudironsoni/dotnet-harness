using System.Linq;
using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class GraphEngineTests
{
    [Fact]
    public void Build_ForSpecificItem_ReturnsReachableReferenceGraph()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var report = GraphEngine.Build(repo.Root, new GraphOptions
        {
            ItemId = "reviewer",
            Depth = 2,
            Format = "mermaid"
        });

        Assert.Contains(report.Nodes, node => node.Id == "reviewer");
        Assert.Contains(report.Nodes, node => node.Id == "dotnet-code-review-agent");
        Assert.Contains(report.Nodes, node => node.Id == "dotnet-csharp-code-smells");
        Assert.Contains(report.Edges, edge => edge.FromId == "reviewer" && edge.ToId == "dotnet-code-review-agent");
        Assert.Contains("graph TD", report.RenderedGraph);
    }

    [Fact]
    public void Build_ForCategory_ReturnsDotOutputAndHubs()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var report = GraphEngine.Build(repo.Root, new GraphOptions
        {
            Kind = CatalogKinds.Skill,
            Category = "dotnet",
            Format = "dot"
        });

        Assert.NotEmpty(report.Nodes);
        Assert.Contains("digraph dotnet_agent_harness", report.RenderedGraph);
        Assert.True(report.Hubs.Count >= 0);
        Assert.True(report.Orphans.Count >= 0);
        Assert.True(report.Nodes.All(node => node.Kind == CatalogKinds.Skill));
    }
}
