using System;
using System.IO;
using DotNetAgentHarness.Evals.Engine;
using DotNetAgentHarness.Evals.Models;
using Xunit;

namespace DotNetAgentHarness.Evals.Tests;

public class EvalArtifactWriterTests
{
    [Fact]
    public void Write_PersistsExpectedArtifactFields()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "dotnet-agent-harness-evals-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var artifactPath = Path.Combine(tempDirectory, "eval-artifact.json");

        try
        {
            var artifact = new EvalRunArtifact
            {
                RunId = "run-123",
                GeneratedAtUtc = new DateTimeOffset(2026, 3, 6, 6, 0, 0, TimeSpan.Zero),
                UseDummyMode = true,
                ModeSource = "default",
                Provider = "openai",
                Model = "gpt-4.1-mini",
                CaseFilePath = "/tmp/routing.yaml",
                DefaultTrialCount = 2,
                Gate = "nightly",
                PolicyProfile = "strict",
                PromptEvidenceId = "prompt-123",
                Overall = new EvalArtifactOverall
                {
                    CaseCount = 1,
                    TrialCount = 2,
                    PassedTrials = 1,
                    FailedTrials = 1,
                    PassRate = 0.5
                },
                Cases =
                [
                    new EvalArtifactCase
                    {
                        CaseId = "case-a",
                        Description = "review routing",
                        Prompt = "Review this code",
                        ExpectedTrigger = "reviewer",
                        TrialCount = 2,
                        PassedTrials = 1,
                        FailedTrials = 1,
                        PassRate = 0.5,
                        AverageElapsedMilliseconds = 12.3,
                        Failures =
                        [
                            new EvalArtifactFailure
                            {
                                TrialNumber = 2,
                                TriggerMessage = "Expected trigger 'reviewer' but got 'implementer'.",
                                Summary = "Trigger mismatch",
                                AssertionMessages = ["Response was missing review findings."]
                            }
                        ]
                    }
                ]
            };

            var fullPath = EvalArtifactWriter.Write(artifactPath, artifact);

            Assert.True(File.Exists(fullPath));
            var content = File.ReadAllText(fullPath);
            Assert.Contains("\"RunId\": \"run-123\"", content);
            Assert.Contains("\"PromptEvidenceId\": \"prompt-123\"", content);
            Assert.Contains("\"Gate\": \"nightly\"", content);
            Assert.Contains("\"PolicyProfile\": \"strict\"", content);
            Assert.Contains("\"CaseId\": \"case-a\"", content);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
