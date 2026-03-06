using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class DoctorEngineTests
{
    [Fact]
    public void BuildReport_FlagsBlockingAndQualityIssues()
    {
        var profile = new RepositoryProfile
        {
            RepoRoot = "/tmp/example",
            Projects =
            [
                new ProjectSummary { Name = "App", Kind = "web", TargetFrameworks = ["net10.0"] },
                new ProjectSummary { Name = "Worker", Kind = "classlib", TargetFrameworks = ["net10.0"] }
            ],
            Solutions = [],
            TargetFrameworks = ["net10.0"],
            HasDirectoryBuildProps = false,
            HasEditorConfig = false,
            HasDotNetToolManifest = false,
            HasRulesync = false,
            CiProviders = [],
            TestProjectCount = 0,
            InstalledSdkVersions = []
        };

        var report = DoctorEngine.BuildReport(profile, new DotNetEnvironmentReport
        {
            IsAvailable = false,
            ErrorMessage = "dotnet missing"
        });

        Assert.Contains(report.Findings, finding => finding.Code == "dotnet-missing" && finding.Severity == "error");
        Assert.Contains(report.Findings, finding => finding.Code == "solution-missing");
        Assert.Contains(report.Findings, finding => finding.Code == "directory-build-props-missing");
        Assert.Contains(report.Findings, finding => finding.Code == "tests-missing");
        Assert.Contains(report.Findings, finding => finding.Code == "rulesync-missing");
    }
}
