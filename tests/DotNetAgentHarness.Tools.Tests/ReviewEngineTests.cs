using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class ReviewEngineTests
{
    [Fact]
    public void Review_FindsCommonDotNetHeuristics()
    {
        using var repo = new TestRepositoryBuilder();
        repo.WriteFile("src/App/Service.cs", """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class Service
            {
                public void Run()
                {
                    var client = new HttpClient();
                    Task.Run(() => { }).Wait();
                }
            }
            """);

        var report = ReviewEngine.Review(repo.Root);

        Assert.Contains(report.Findings, finding => finding.RuleId == "new-httpclient");
        Assert.Contains(report.Findings, finding => finding.RuleId == "sync-over-async");
        Assert.Contains(report.Findings, finding => finding.RuleId == "task-run");
    }

    [Fact]
    public void Review_IgnoresMatchesInsideStringsAndComments()
    {
        using var repo = new TestRepositoryBuilder();
        repo.WriteFile("src/App/Generated.cs", """
            using System;

            public static class Generated
            {
                private const string AsyncPattern = ".Result";

                public static void Log()
                {
                    // BuildServiceProvider()
                    Console.WriteLine("Task.Run(() => { })");
                }
            }
            """);

        var report = ReviewEngine.Review(repo.Root);

        Assert.Empty(report.Findings);
    }

    [Fact]
    public void Review_FindsExceptionHandlingAndLinqHeuristics()
    {
        using var repo = new TestRepositoryBuilder();
        repo.WriteFile("src/App/Handler.cs", """
            using System;
            using System.Linq;

            public static class Handler
            {
                public static bool Run(int[] values)
                {
                    try
                    {
                        return values.Count() > 0;
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
            }
            """);

        var report = ReviewEngine.Review(repo.Root);

        Assert.Contains(report.Findings, finding => finding.RuleId == "catch-general-exception");
        Assert.Contains(report.Findings, finding => finding.RuleId == "throw-ex");
        Assert.Contains(report.Findings, finding => finding.RuleId == "count-greater-than-zero");
    }
}
