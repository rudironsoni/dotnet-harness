using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotNetAgentHarness.Tools.Engine;

public static class ValidationEngine
{
    public static ValidationReport Validate(string repoRoot, string mode)
    {
        return Validate(repoRoot, mode, new ValidationOptions());
    }

    public static ValidationReport Validate(string repoRoot, string mode, ValidationOptions options)
    {
        var normalizedMode = string.IsNullOrWhiteSpace(mode) ? "all" : mode.Trim().ToLowerInvariant();
        var checks = new List<ValidationCheck>();
        var target = string.Empty;

        if (normalizedMode is "all" or "skill" or "skills" or "catalog")
        {
            var lintResult = FrontmatterLinter.Lint(repoRoot);
            checks.Add(new ValidationCheck
            {
                Name = "frontmatter",
                Passed = lintResult.Passed,
                Message = lintResult.Passed
                    ? "Frontmatter validation passed."
                    : $"{lintResult.Errors.Count} error(s), {lintResult.Warnings.Count} warning(s)."
            });

            try
            {
                var skillsDir = Path.Combine(repoRoot, ".rulesync", "skills");
                var manifest = ManifestBuilder.Build(skillsDir);
                checks.Add(new ValidationCheck
                {
                    Name = "skill-manifest",
                    Passed = manifest.Errors.Count == 0,
                    Message = manifest.Errors.Count == 0
                        ? $"Built manifest for {manifest.Stats.TotalSkills} skill(s)."
                        : $"Manifest build reported {manifest.Errors.Count} error(s)."
                });

                var catalog = ToolkitCatalogLoader.Load(repoRoot);
                checks.Add(new ValidationCheck
                {
                    Name = "toolkit-catalog",
                    Passed = catalog.Stats.TotalItems > 0,
                    Message = $"Loaded {catalog.Stats.TotalItems} catalog item(s)."
                });

                var personas = PersonaCatalogLoader.Load(repoRoot);
                checks.Add(new ValidationCheck
                {
                    Name = "persona-catalog",
                    Passed = personas.Personas.Count > 0,
                    Message = $"Loaded {personas.Personas.Count} persona descriptor(s)."
                });

                checks.AddRange(PromptBundleEvalSuite.Run(repoRoot));

                var skillTests = SkillTestEngine.Run(repoRoot, "all", failFast: false);
                checks.Add(new ValidationCheck
                {
                    Name = "skill-tests",
                    Passed = skillTests.Passed,
                    Message = skillTests.Passed
                        ? $"Executed {skillTests.TotalChecks} skill check(s) across {skillTests.TotalCases} case(s); {skillTests.SkillsWithoutCases} skill(s) still have no authored cases."
                        : $"{skillTests.FailedChecks} of {skillTests.TotalChecks} skill check(s) failed."
                });
            }
            catch (Exception ex)
            {
                checks.Add(new ValidationCheck
                {
                    Name = "catalog-load",
                    Passed = false,
                    Message = ex.Message
                });
            }
        }

        if (normalizedMode is "all" or "repo")
        {
            var environment = ProjectAnalyzer.ProbeDotNetEnvironment();
            var profile = ProjectAnalyzer.Analyze(repoRoot, environment);
            var doctor = DoctorEngine.BuildReport(profile);

            checks.Add(new ValidationCheck
            {
                Name = "repo-analysis",
                Passed = profile.Projects.Count > 0 || profile.Solutions.Count > 0,
                Severity = profile.Projects.Count > 0 || profile.Solutions.Count > 0 ? "info" : "error",
                Message = $"Detected {profile.Projects.Count} project(s) and {profile.Solutions.Count} solution file(s)."
            });

            checks.Add(new ValidationCheck
            {
                Name = "repo-doctor",
                Passed = doctor.Passed,
                Severity = doctor.Passed ? "info" : "error",
                Message = doctor.Passed
                    ? "Repository doctor found no blocking issues."
                    : $"{doctor.Findings.Count} issue(s) reported.",
                Evidence = string.Join(Environment.NewLine, doctor.Findings.Select(finding => $"[{finding.Severity}] {finding.Code}: {finding.Message}")),
                Remediation = string.Join(Environment.NewLine, doctor.Findings.Select(finding => finding.Remediation).Where(text => !string.IsNullOrWhiteSpace(text)).Distinct(StringComparer.OrdinalIgnoreCase))
            });

            var runtimeChecks = RepoValidationEngine.Validate(repoRoot, profile, options, environment).ToList();
            var targetCheck = runtimeChecks.FirstOrDefault(check => check.Name == "dotnet-target");
            if (targetCheck is not null && targetCheck.Passed)
            {
                target = targetCheck.Message.Replace("Runtime validation target: ", string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            checks.AddRange(runtimeChecks);
        }

        if (normalizedMode is "all" or "eval")
        {
            var evalCasePath = Path.Combine(repoRoot, "tests", "eval", "cases", "routing.yaml");
            checks.Add(new ValidationCheck
            {
                Name = "eval-cases",
                Passed = File.Exists(evalCasePath) && new FileInfo(evalCasePath).Length > 0,
                Message = File.Exists(evalCasePath)
                    ? $"Eval cases found at {evalCasePath}."
                    : "Eval case file tests/eval/cases/routing.yaml is missing."
            });

            var evalProjectPath = Path.Combine(repoRoot, "src", "DotNetAgentHarness.Evals", "DotNetAgentHarness.Evals.csproj");
            checks.Add(new ValidationCheck
            {
                Name = "eval-project",
                Passed = File.Exists(evalProjectPath),
                Message = File.Exists(evalProjectPath)
                    ? "Eval project is present."
                    : "Eval project file is missing."
            });
        }

        return new ValidationReport
        {
            Mode = normalizedMode,
            Target = target,
            Checks = checks
        };
    }
}
