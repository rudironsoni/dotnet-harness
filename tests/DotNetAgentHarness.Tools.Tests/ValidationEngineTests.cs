using System.IO;
using System.Linq;
using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class ValidationEngineTests
{
    [Fact]
    public void Validate_RunDotNet_BuildsSimpleConsoleRepository()
    {
        using var repo = new TestRepositoryBuilder();
        CreateConsoleRepository(repo, "SampleApp");

        var report = ValidationEngine.Validate(repo.Root, "repo", new ValidationOptions
        {
            RunDotNet = true,
            TimeoutMs = 120_000
        });

        Assert.True(report.Passed);
        Assert.True(
            report.Target.EndsWith("SampleApp.sln", System.StringComparison.OrdinalIgnoreCase)
            || report.Target.EndsWith("SampleApp.slnx", System.StringComparison.OrdinalIgnoreCase),
            $"Unexpected validation target '{report.Target}'.");
        Assert.Contains(report.Checks, check => check.Name == "dotnet-target" && check.Passed);
        Assert.Contains(report.Checks, check => check.Name == "dotnet-restore" && check.Passed);
        Assert.Contains(report.Checks, check => check.Name == "dotnet-build" && check.Passed);
        Assert.Contains(report.Checks, check => check.Name == "dotnet-test-skipped" && check.Passed);
    }

    [Fact]
    public void Validate_RunDotNet_ReportsCompilerDiagnostics()
    {
        using var repo = new TestRepositoryBuilder();
        CreateConsoleRepository(repo, "BrokenApp");
        repo.WriteFile("src/BrokenApp/Program.cs", """
            Console.WriteLine("broken")
            """);

        var report = ValidationEngine.Validate(repo.Root, "repo", new ValidationOptions
        {
            RunDotNet = true,
            SkipTest = true,
            TimeoutMs = 120_000
        });

        Assert.False(report.Passed);
        var buildCheck = Assert.Single(report.Checks, check => check.Name == "dotnet-build");
        Assert.False(buildCheck.Passed);
        Assert.Contains("CS", buildCheck.Message);
        Assert.Contains("compiler error", buildCheck.Remediation, System.StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(buildCheck.Evidence));
    }

    private static void CreateConsoleRepository(TestRepositoryBuilder repo, string solutionName)
    {
        ProcessRunner.Run("dotnet", $"new sln -n {solutionName}", repo.Root, 120_000);
        ProcessRunner.Run("dotnet", $"new console -n {solutionName} -o src/{solutionName}", repo.Root, 120_000);
        var solutionPath = Directory.EnumerateFiles(repo.Root, $"{solutionName}.sln*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Single();
        ProcessRunner.Run("dotnet", $"sln {solutionPath} add src/{solutionName}/{solutionName}.csproj", repo.Root, 120_000);
    }
}
