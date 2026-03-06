using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class ToolkitCatalogLoaderTests
{
    [Fact]
    public void Load_IncludesPersonasInCatalogStats()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var catalog = ToolkitCatalogLoader.Load(repo.Root);

        Assert.Equal(4, catalog.Stats.Personas);
        Assert.Contains(catalog.Items, item => item.Kind == CatalogKinds.Persona && item.Id == "reviewer");
        Assert.Contains(catalog.Items, item => item.Kind == CatalogKinds.Persona && item.References.Contains("dotnet-code-review-agent"));
    }

    [Fact]
    public void SearchAndCompare_WorkForPersonaItems()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var catalog = ToolkitCatalogLoader.Load(repo.Root);
        var searchResults = ToolkitCatalogLoader.Search(catalog, new CatalogSearchQuery
        {
            Query = "review",
            Kind = CatalogKinds.Persona,
            Limit = 5
        });

        Assert.Contains(searchResults, result => result.Item.Id == "reviewer");

        var comparison = ToolkitCatalogLoader.Compare(catalog, "reviewer", "implementer");

        Assert.Equal(CatalogKinds.Persona, comparison.Left.Kind);
        Assert.Equal(CatalogKinds.Persona, comparison.Right.Kind);
        Assert.Contains("persona", comparison.SharedTags);
    }
}
