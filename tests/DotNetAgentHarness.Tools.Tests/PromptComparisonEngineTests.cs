using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class PromptComparisonEngineTests
{
    [Fact]
    public void Compare_FindsChangedRequestSectionBetweenEvidenceArtifacts()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var left = PromptBundleBuilder.Prepare(repo.Root, "Review the repo for async regressions", new PromptAssemblyOptions
        {
            PersonaId = "reviewer",
            TargetPath = "src/App/App.csproj",
            Platform = PromptPlatforms.CodexCli
        });
        RepoStateStore.WritePreparedMessageEvidence(repo.Root, left, "left-review");

        var right = PromptBundleBuilder.Prepare(repo.Root, "Review the repo for security regressions", new PromptAssemblyOptions
        {
            PersonaId = "reviewer",
            TargetPath = "src/App/App.csproj",
            Platform = PromptPlatforms.CodexCli
        });
        RepoStateStore.WritePreparedMessageEvidence(repo.Root, right, "right-review");

        var comparison = PromptComparisonEngine.Compare(repo.Root, "left-review", "right-review");

        Assert.Equal("left-review", comparison.LeftEvidenceId);
        Assert.Equal("right-review", comparison.RightEvidenceId);
        Assert.True(comparison.SamePersona);
        Assert.True(comparison.SamePlatform);
        Assert.Contains("request", comparison.ChangedSections);
        Assert.DoesNotContain("tool", comparison.ChangedSections);
        var request = Assert.Single(comparison.Sections, section => section.SectionName == "request");
        Assert.False(request.IsIdentical);
        Assert.NotNull(request.FirstDifferenceLine);
        Assert.Contains(request.LeftOnlyLines, line => line.Contains("async regressions"));
        Assert.Contains(request.RightOnlyLines, line => line.Contains("security regressions"));
    }
}
