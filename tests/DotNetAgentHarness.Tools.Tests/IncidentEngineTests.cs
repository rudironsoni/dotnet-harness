using System.IO;
using DotNetAgentHarness.Tools.Engine;
using Xunit;

namespace DotNetAgentHarness.Tools.Tests;

public class IncidentEngineTests
{
    [Fact]
    public void AddPromptIncident_WritesIncidentWithPromptReference()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var report = PromptBundleBuilder.Prepare(repo.Root, "Review the repo for validation regressions", new PromptAssemblyOptions
        {
            PersonaId = "reviewer",
            TargetPath = "src/App/App.csproj",
            Platform = PromptPlatforms.ClaudeCode
        });
        RepoStateStore.WritePreparedMessageEvidence(repo.Root, report, "failing-review");

        var incident = IncidentEngine.AddPromptIncident(repo.Root, "Prompt caused reviewer misroute", "failing-review", new PromptIncidentCreateOptions
        {
            IncidentId = "incident-reviewer-misroute",
            Severity = "high",
            Owner = "platform-team",
            Notes = "Raised from regression capture."
        });

        Assert.Equal("incident-reviewer-misroute", incident.IncidentId);
        Assert.Equal("high", incident.Severity);
        Assert.Equal("reviewer", incident.PersonaId);
        Assert.Equal("claudecode", incident.Platform);
        Assert.Equal("failing-review", incident.PromptEvidence.EvidenceId);
        Assert.True(File.Exists(incident.FilePath));
        var content = File.ReadAllText(incident.FilePath);
        Assert.Contains("\"PromptEvidence\"", content);
        Assert.Contains("\"EvidenceId\": \"failing-review\"", content);
        Assert.Contains("\"Owner\": \"platform-team\"", content);
    }

    [Fact]
    public void ListAndShowIncident_ReturnSavedIncident()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var report = PromptBundleBuilder.Prepare(repo.Root, "Review the repo for validation regressions", new PromptAssemblyOptions
        {
            PersonaId = "reviewer",
            TargetPath = "src/App/App.csproj",
            Platform = PromptPlatforms.ClaudeCode
        });
        RepoStateStore.WritePreparedMessageEvidence(repo.Root, report, "listed-review");
        IncidentEngine.AddPromptIncident(repo.Root, "Prompt listing check", "listed-review", new PromptIncidentCreateOptions
        {
            IncidentId = "incident-listing-check",
            Severity = "medium"
        });

        var incidents = IncidentEngine.ListIncidents(repo.Root);
        var listed = Assert.Single(incidents, item => item.IncidentId == "incident-listing-check");
        var shown = IncidentEngine.ShowIncident(repo.Root, "incident-listing-check");

        Assert.Equal("Prompt listing check", listed.Title);
        Assert.Equal("incident-listing-check", shown.IncidentId);
        Assert.Equal("listed-review", shown.PromptEvidence.EvidenceId);
    }

    [Fact]
    public void AddPromptIncidentFromEvalArtifact_AttachesEvalContext()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var report = PromptBundleBuilder.Prepare(repo.Root, "Review the repo for validation regressions", new PromptAssemblyOptions
        {
            PersonaId = "reviewer",
            TargetPath = "src/App/App.csproj",
            Platform = PromptPlatforms.CodexCli
        });
        RepoStateStore.WritePreparedMessageEvidence(repo.Root, report, "eval-linked-review");
        repo.WriteFile(".dotnet-agent-harness/evidence/evals/failing-run.json", """
            {
              "runId": "run-789",
              "useDummyMode": true,
              "provider": "openai",
              "model": "gpt-4.1-mini",
              "caseFilePath": "tests/eval/cases/routing.yaml",
              "gate": "nightly",
              "policyProfile": "strict",
              "promptEvidenceId": "eval-linked-review",
              "overall": {
                "caseCount": 1,
                "trialCount": 2,
                "failedTrials": 1,
                "passRate": 0.5
              },
              "cases": [
                {
                  "caseId": "routing-reviewer",
                  "description": "review routing",
                  "expectedTrigger": "reviewer",
                  "passRate": 0.5,
                  "failedTrials": 1,
                  "failures": [
                    {
                      "summary": "Trigger mismatch"
                    }
                  ]
                }
              ]
            }
            """);

        var incident = IncidentEngine.AddPromptIncidentFromEvalArtifact(repo.Root, "failing-run", new PromptIncidentCreateOptions
        {
            IncidentId = "incident-from-eval",
            Owner = "eval-bot"
        });

        Assert.Equal("incident-from-eval", incident.IncidentId);
        Assert.Equal("high", incident.Severity);
        Assert.NotNull(incident.EvalContext);
        Assert.Equal("run-789", incident.EvalContext!.RunId);
        Assert.Equal("strict", incident.EvalContext.PolicyProfile);
        Assert.Equal("eval-linked-review", incident.PromptEvidence.EvidenceId);
        Assert.Single(incident.EvalContext.FailedCases);
        Assert.Equal("routing-reviewer", incident.EvalContext.FailedCases[0].CaseId);
    }

    [Fact]
    public void ResolveIncident_PersistsResolutionMetadata()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var report = PromptBundleBuilder.Prepare(repo.Root, "Review the repo for validation regressions", new PromptAssemblyOptions
        {
            PersonaId = "reviewer",
            TargetPath = "src/App/App.csproj",
            Platform = PromptPlatforms.CodexCli
        });
        RepoStateStore.WritePreparedMessageEvidence(repo.Root, report, "resolve-review");
        IncidentEngine.AddPromptIncident(repo.Root, "Resolver check", "resolve-review", new PromptIncidentCreateOptions
        {
            IncidentId = "incident-resolve-check",
            Severity = "high"
        });

        var resolved = IncidentEngine.ResolveIncident(repo.Root, "incident-resolve-check", new PromptIncidentResolutionOptions
        {
            Owner = "platform-team",
            Rationale = "Prompt routing was fixed and covered by regression eval.",
            RegressionCaseId = "routing-reviewer-001",
            RegressionCasePath = "tests/eval/cases/routing.yaml",
            Notes = "Validated in nightly."
        });

        Assert.Equal("resolved", resolved.Status);
        Assert.NotNull(resolved.Resolution);
        Assert.Equal("platform-team", resolved.Resolution!.Owner);
        Assert.Equal("routing-reviewer-001", resolved.Resolution.RegressionCaseId);
        Assert.Equal("tests/eval/cases/routing.yaml", resolved.Resolution.RegressionCasePath);

        var persisted = IncidentEngine.ShowIncident(repo.Root, "incident-resolve-check");
        Assert.Equal("resolved", persisted.Status);
        Assert.Equal("routing-reviewer-001", persisted.Resolution!.RegressionCaseId);
    }

    [Fact]
    public void CloseIncident_PersistsClosedStatus()
    {
        using var repo = new TestRepositoryBuilder();
        ToolkitTestContent.WritePromptToolkit(repo);

        var report = PromptBundleBuilder.Prepare(repo.Root, "Review the repo for validation regressions", new PromptAssemblyOptions
        {
            PersonaId = "reviewer",
            TargetPath = "src/App/App.csproj",
            Platform = PromptPlatforms.ClaudeCode
        });
        RepoStateStore.WritePreparedMessageEvidence(repo.Root, report, "close-review");
        IncidentEngine.AddPromptIncident(repo.Root, "Closer check", "close-review", new PromptIncidentCreateOptions
        {
            IncidentId = "incident-close-check"
        });

        var closed = IncidentEngine.CloseIncident(repo.Root, "incident-close-check", new PromptIncidentResolutionOptions
        {
            Owner = "eval-bot",
            Rationale = "Superseded by the permanent regression case.",
            RegressionCaseId = "incident-close-regression"
        });

        Assert.Equal("closed", closed.Status);
        Assert.NotNull(closed.Resolution);
        Assert.Equal("closed", closed.Resolution!.Status);
        Assert.Equal("eval-bot", closed.Resolution.Owner);
    }
}
