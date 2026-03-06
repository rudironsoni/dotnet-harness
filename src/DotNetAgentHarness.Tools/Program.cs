using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DotNetAgentHarness.Tools.Engine;

namespace DotNetAgentHarness.Tools;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        try
        {
            var command = args[0].ToLowerInvariant();
            var cliArgs = new CliArguments(args.Skip(1));
            var repoRoot = ResolveRepoRoot(cliArgs);

            return command switch
            {
                "lint-frontmatter" => RunLintFrontmatter(repoRoot),
                "build-manifest" => RunBuildManifest(repoRoot, cliArgs),
                "build-catalog" => RunBuildCatalog(repoRoot, cliArgs),
                "analyze" => RunAnalyze(repoRoot, cliArgs),
                "recommend" => RunRecommend(repoRoot, cliArgs),
                "init" => RunInit(repoRoot, cliArgs),
                "doctor" => RunDoctor(repoRoot, cliArgs),
                "validate" => RunValidate(repoRoot, cliArgs),
                "search" => RunSearch(repoRoot, cliArgs),
                "profile" => RunProfile(repoRoot, cliArgs),
                "compare" => RunCompare(repoRoot, cliArgs),
                "compare-prompts" => RunComparePrompts(repoRoot, cliArgs),
                "prepare-message" => RunPrepareMessage(repoRoot, cliArgs),
                "incident" => RunIncident(repoRoot, cliArgs),
                "review" => RunReview(repoRoot, cliArgs),
                "test" => RunTest(repoRoot, cliArgs),
                "scaffold" => RunScaffold(repoRoot, cliArgs),
                "help" or "--help" or "-h" => PrintHelpAndReturn(),
                _ => Fail($"Unknown command '{command}'.")
            };
        }
        catch (ArgumentException ex)
        {
            return Fail(ex.Message);
        }
        catch (DirectoryNotFoundException ex)
        {
            return Fail(ex.Message);
        }
    }

    private static int RunBuildManifest(string repoRoot, CliArguments cliArgs)
    {
        var rulesyncDir = Path.Combine(repoRoot, ".rulesync");
        var skillsDir = Path.Combine(rulesyncDir, "skills");
        var outputDir = Path.Combine(rulesyncDir, "manifest");
        Directory.CreateDirectory(outputDir);
        var outputPath = cliArgs.GetOption("--output") ?? Path.Combine(outputDir, "skill-manifest.json");
        EnsureParentDirectory(outputPath);

        var manifest = ManifestBuilder.Build(skillsDir);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);

        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Manifest written to {outputPath}");
        return 0;
    }

    private static int RunBuildCatalog(string repoRoot, CliArguments cliArgs)
    {
        var rulesyncDir = Path.Combine(repoRoot, ".rulesync");
        var outputPath = cliArgs.GetOption("--output") ?? Path.Combine(rulesyncDir, "manifest", "toolkit-catalog.json");
        var catalog = ToolkitCatalogLoader.Load(repoRoot);
        EnsureParentDirectory(outputPath);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(catalog, JsonOptions));
        Console.WriteLine($"Catalog written to {outputPath}");
        return 0;
    }

    private static int RunLintFrontmatter(string repoRoot)
    {
        var result = FrontmatterLinter.Lint(repoRoot);
        Console.WriteLine($"Errors: {result.Errors.Count}");
        Console.WriteLine($"Warnings: {result.Warnings.Count}");
        foreach (var error in result.Errors)
        {
            Console.WriteLine($"ERROR: {error}");
        }

        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"WARN: {warning}");
        }

        return result.Passed ? 0 : 1;
    }

    private static int RunAnalyze(string repoRoot, CliArguments cliArgs)
    {
        var profile = ProjectAnalyzer.Analyze(repoRoot);
        if (cliArgs.HasFlag("--write-state"))
        {
            var path = RepoStateStore.WriteJson(repoRoot, Path.Combine(".dotnet-agent-harness", "project-profile.json"), profile);
            Console.WriteLine($"Profile written to {path}");
        }

        return WriteOutput(profile, cliArgs, textWriter: WriteProfileSummary);
    }

    private static int RunRecommend(string repoRoot, CliArguments cliArgs)
    {
        var profile = LoadOrAnalyzeProfile(repoRoot, cliArgs);
        var catalog = ToolkitCatalogLoader.Load(repoRoot);
        var bundle = RecommendationEngine.Recommend(profile, catalog, cliArgs.GetIntOption("--limit", 5));

        if (cliArgs.HasFlag("--write-state"))
        {
            var path = RepoStateStore.WriteJson(repoRoot, Path.Combine(".dotnet-agent-harness", "recommendations.json"), bundle);
            Console.WriteLine($"Recommendations written to {path}");
        }

        return WriteOutput(bundle, cliArgs, textWriter: WriteRecommendationSummary);
    }

    private static int RunInit(string repoRoot, CliArguments cliArgs)
    {
        var environment = ProjectAnalyzer.ProbeDotNetEnvironment();
        var profile = ProjectAnalyzer.Analyze(repoRoot, environment);
        var catalog = ToolkitCatalogLoader.Load(repoRoot);
        var recommendations = RecommendationEngine.Recommend(profile, catalog, cliArgs.GetIntOption("--limit", 5));
        var doctor = DoctorEngine.BuildReport(profile, environment);
        var report = new InitReport
        {
            Profile = profile,
            Recommendations = recommendations,
            Doctor = doctor
        };

        if (!cliArgs.HasFlag("--no-save"))
        {
            RepoStateStore.WriteJson(repoRoot, Path.Combine(".dotnet-agent-harness", "project-profile.json"), profile);
            RepoStateStore.WriteJson(repoRoot, Path.Combine(".dotnet-agent-harness", "recommendations.json"), recommendations);
            RepoStateStore.WriteJson(repoRoot, Path.Combine(".dotnet-agent-harness", "doctor-report.json"), doctor);
        }

        return WriteOutput(report, cliArgs, textWriter: WriteInitSummary);
    }

    private static int RunDoctor(string repoRoot, CliArguments cliArgs)
    {
        var environment = ProjectAnalyzer.ProbeDotNetEnvironment();
        var profile = LoadOrAnalyzeProfile(repoRoot, cliArgs, environment);
        var report = DoctorEngine.BuildReport(profile, environment);

        if (cliArgs.HasFlag("--write-state"))
        {
            var path = RepoStateStore.WriteJson(repoRoot, Path.Combine(".dotnet-agent-harness", "doctor-report.json"), report);
            Console.WriteLine($"Doctor report written to {path}");
        }

        return WriteOutput(report, cliArgs, textWriter: WriteDoctorSummary);
    }

    private static int RunValidate(string repoRoot, CliArguments cliArgs)
    {
        var mode = cliArgs.GetOption("--mode") ?? "all";
        var report = ValidationEngine.Validate(repoRoot, mode, new ValidationOptions
        {
            RunDotNet = cliArgs.HasFlag("--run"),
            SkipRestore = cliArgs.HasFlag("--skip-restore"),
            SkipBuild = cliArgs.HasFlag("--skip-build"),
            SkipTest = cliArgs.HasFlag("--skip-test"),
            Configuration = cliArgs.GetOption("--configuration") ?? "Debug",
            Framework = cliArgs.GetOption("--framework"),
            TargetPath = cliArgs.GetOption("--target"),
            TimeoutMs = cliArgs.GetIntOption("--timeout-ms", 120_000)
        });
        return WriteOutput(report, cliArgs, textWriter: WriteValidationSummary, failureExitCode: report.Passed ? 0 : 1);
    }

    private static int RunSearch(string repoRoot, CliArguments cliArgs)
    {
        var query = string.Join(' ', cliArgs.Positionals).Trim();
        var catalog = ToolkitCatalogLoader.Load(repoRoot);
        var results = ToolkitCatalogLoader.Search(catalog, new CatalogSearchQuery
        {
            Query = query,
            Category = cliArgs.GetOption("--category"),
            Platform = cliArgs.GetOption("--platform"),
            Kind = cliArgs.GetOption("--kind"),
            Limit = cliArgs.GetIntOption("--limit", 10)
        });

        return WriteOutput(results, cliArgs, textWriter: WriteSearchSummary);
    }

    private static int RunProfile(string repoRoot, CliArguments cliArgs)
    {
        var started = DateTimeOffset.UtcNow;
        var catalog = ToolkitCatalogLoader.Load(repoRoot);
        var elapsed = DateTimeOffset.UtcNow - started;

        if (cliArgs.Positionals.Count == 0)
        {
            var payload = new
            {
                catalog.Stats,
                load_time_ms = elapsed.TotalMilliseconds
            };
            return WriteOutput(payload, cliArgs, textWriter: _ =>
            {
                Console.WriteLine($"Catalog items: {catalog.Stats.TotalItems}");
                Console.WriteLine($"Skills: {catalog.Stats.Skills}");
                Console.WriteLine($"Subagents: {catalog.Stats.Subagents}");
                Console.WriteLine($"Commands: {catalog.Stats.Commands}");
                Console.WriteLine($"Personas: {catalog.Stats.Personas}");
                Console.WriteLine($"Total lines: {catalog.Stats.TotalLines}");
                Console.WriteLine($"Load time: {elapsed.TotalMilliseconds:F1}ms");
            });
        }

        var itemId = cliArgs.Positionals[0];
        var item = catalog.Find(itemId) ?? throw new ArgumentException($"Catalog item '{itemId}' was not found.");
        return WriteOutput(item, cliArgs, textWriter: _ =>
        {
            Console.WriteLine($"{item.Id} ({item.Kind})");
            Console.WriteLine($"Name: {item.Name}");
            Console.WriteLine($"File: {item.FilePath}");
            Console.WriteLine($"Lines: {item.LineCount}");
            Console.WriteLine($"Approx tokens: {item.ApproximateTokens}");
            Console.WriteLine($"Platforms: {string.Join(", ", item.Platforms)}");
            Console.WriteLine($"Tags: {string.Join(", ", item.Tags)}");
            Console.WriteLine($"References: {string.Join(", ", item.References)}");
        });
    }

    private static int RunCompare(string repoRoot, CliArguments cliArgs)
    {
        if (cliArgs.Positionals.Count < 2)
        {
            throw new ArgumentException("compare requires two catalog item ids.");
        }

        var catalog = ToolkitCatalogLoader.Load(repoRoot);
        var comparison = ToolkitCatalogLoader.Compare(catalog, cliArgs.Positionals[0], cliArgs.Positionals[1]);
        return WriteOutput(comparison, cliArgs, textWriter: _ =>
        {
            Console.WriteLine($"{comparison.Left.Id} vs {comparison.Right.Id}");
            Console.WriteLine($"Kinds: {comparison.Left.Kind} / {comparison.Right.Kind}");
            Console.WriteLine($"Lines: {comparison.Left.LineCount} / {comparison.Right.LineCount}");
            Console.WriteLine($"Tokens: {comparison.Left.ApproximateTokens} / {comparison.Right.ApproximateTokens}");
            Console.WriteLine($"Shared tags: {string.Join(", ", comparison.SharedTags)}");
            Console.WriteLine($"Only {comparison.Left.Id}: {string.Join(", ", comparison.UniqueToLeft)}");
            Console.WriteLine($"Only {comparison.Right.Id}: {string.Join(", ", comparison.UniqueToRight)}");
        });
    }

    private static int RunPrepareMessage(string repoRoot, CliArguments cliArgs)
    {
        var rawRequest = string.Join(' ', cliArgs.Positionals).Trim();
        if (string.IsNullOrWhiteSpace(rawRequest))
        {
            throw new ArgumentException("prepare-message requires a user request.");
        }

        var report = PromptBundleBuilder.Prepare(repoRoot, rawRequest, new PromptAssemblyOptions
        {
            PersonaId = cliArgs.GetOption("--persona"),
            TargetPath = cliArgs.GetOption("--target"),
            SkillLimit = cliArgs.GetIntOption("--limit", 6),
            Platform = cliArgs.GetOption("--platform") ?? PromptPlatforms.Generic
        });

        if (cliArgs.HasFlag("--write-evidence"))
        {
            report.Evidence = RepoStateStore.WritePreparedMessageEvidence(repoRoot, report, cliArgs.GetOption("--evidence-id"));
        }

        var format = (cliArgs.GetOption("--format") ?? "text").ToLowerInvariant();
        if (format == "prompt")
        {
            if (report.Evidence is not null)
            {
                Console.Error.WriteLine($"Evidence report: {report.Evidence.ReportPath}");
                Console.Error.WriteLine($"Evidence prompt: {report.Evidence.PromptPath}");
            }

            var outputPath = cliArgs.GetOption("--output");
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                EnsureParentDirectory(outputPath);
                File.WriteAllText(outputPath, report.RenderedPrompt.CompositeText);
                Console.WriteLine(outputPath);
            }
            else
            {
                Console.WriteLine(report.RenderedPrompt.CompositeText);
            }

            return 0;
        }

        return WriteOutput(report, cliArgs, textWriter: WritePreparedMessageSummary);
    }

    private static int RunComparePrompts(string repoRoot, CliArguments cliArgs)
    {
        if (cliArgs.Positionals.Count < 2)
        {
            throw new ArgumentException("compare-prompts requires two prompt evidence ids.");
        }

        var comparison = PromptComparisonEngine.Compare(repoRoot, cliArgs.Positionals[0], cliArgs.Positionals[1]);
        return WriteOutput(comparison, cliArgs, textWriter: WritePromptComparisonSummary);
    }

    private static int RunIncident(string repoRoot, CliArguments cliArgs)
    {
        if (cliArgs.Positionals.Count == 0)
        {
            throw new ArgumentException("incident requires a subcommand. Supported: add, list, show, from-eval, resolve, close.");
        }

        var action = cliArgs.Positionals[0].ToLowerInvariant();
        return action switch
        {
            "add" => RunIncidentAdd(repoRoot, cliArgs),
            "list" => RunIncidentList(repoRoot, cliArgs),
            "show" => RunIncidentShow(repoRoot, cliArgs),
            "from-eval" => RunIncidentFromEval(repoRoot, cliArgs),
            "resolve" => RunIncidentResolve(repoRoot, cliArgs),
            "close" => RunIncidentClose(repoRoot, cliArgs),
            _ => throw new ArgumentException($"Unsupported incident subcommand '{action}'. Supported: add, list, show, from-eval, resolve, close.")
        };
    }

    private static int RunIncidentAdd(string repoRoot, CliArguments cliArgs)
    {
        if (cliArgs.Positionals.Count < 2)
        {
            throw new ArgumentException("incident add requires a title.");
        }

        var title = string.Join(' ', cliArgs.Positionals.Skip(1)).Trim();
        var promptEvidenceId = cliArgs.GetOption("--prompt-evidence");
        if (string.IsNullOrWhiteSpace(promptEvidenceId))
        {
            throw new ArgumentException("incident add requires --prompt-evidence <id>.");
        }

        var record = IncidentEngine.AddPromptIncident(repoRoot, title, promptEvidenceId, new PromptIncidentCreateOptions
        {
            IncidentId = cliArgs.GetOption("--incident-id"),
            Severity = cliArgs.GetOption("--severity") ?? "medium",
            Owner = cliArgs.GetOption("--owner") ?? string.Empty,
            Notes = cliArgs.GetOption("--notes") ?? string.Empty
        });

        return WriteOutput(record, cliArgs, textWriter: WriteIncidentSummary);
    }

    private static int RunIncidentList(string repoRoot, CliArguments cliArgs)
    {
        var incidents = IncidentEngine.ListIncidents(repoRoot);
        return WriteOutput(incidents, cliArgs, textWriter: WriteIncidentListSummary);
    }

    private static int RunIncidentShow(string repoRoot, CliArguments cliArgs)
    {
        if (cliArgs.Positionals.Count < 2)
        {
            throw new ArgumentException("incident show requires an incident id.");
        }

        var record = IncidentEngine.ShowIncident(repoRoot, cliArgs.Positionals[1]);
        return WriteOutput(record, cliArgs, textWriter: WriteIncidentSummary);
    }

    private static int RunIncidentFromEval(string repoRoot, CliArguments cliArgs)
    {
        if (cliArgs.Positionals.Count < 2)
        {
            throw new ArgumentException("incident from-eval requires an eval artifact path or id.");
        }

        var record = IncidentEngine.AddPromptIncidentFromEvalArtifact(repoRoot, cliArgs.Positionals[1], new PromptIncidentCreateOptions
        {
            IncidentId = cliArgs.GetOption("--incident-id"),
            Severity = cliArgs.GetOption("--severity") ?? "medium",
            Owner = cliArgs.GetOption("--owner") ?? string.Empty,
            Notes = cliArgs.GetOption("--notes") ?? string.Empty,
            PromptEvidenceId = cliArgs.GetOption("--prompt-evidence")
        });

        return WriteOutput(record, cliArgs, textWriter: WriteIncidentSummary);
    }

    private static int RunIncidentResolve(string repoRoot, CliArguments cliArgs)
    {
        if (cliArgs.Positionals.Count < 2)
        {
            throw new ArgumentException("incident resolve requires an incident id.");
        }

        var record = IncidentEngine.ResolveIncident(repoRoot, cliArgs.Positionals[1], BuildIncidentResolutionOptions(cliArgs));
        return WriteOutput(record, cliArgs, textWriter: WriteIncidentSummary);
    }

    private static int RunIncidentClose(string repoRoot, CliArguments cliArgs)
    {
        if (cliArgs.Positionals.Count < 2)
        {
            throw new ArgumentException("incident close requires an incident id.");
        }

        var record = IncidentEngine.CloseIncident(repoRoot, cliArgs.Positionals[1], BuildIncidentResolutionOptions(cliArgs));
        return WriteOutput(record, cliArgs, textWriter: WriteIncidentSummary);
    }

    private static int RunReview(string repoRoot, CliArguments cliArgs)
    {
        var relativePath = cliArgs.Positionals.Count > 0 ? cliArgs.Positionals[0] : null;
        var report = ReviewEngine.Review(repoRoot, relativePath, cliArgs.GetIntOption("--limit", 100));
        return WriteOutput(report, cliArgs, textWriter: WriteReviewSummary);
    }

    private static int RunTest(string repoRoot, CliArguments cliArgs)
    {
        var skillName = cliArgs.Positionals.Count > 0 ? cliArgs.Positionals[0] : "all";
        var suite = SkillTestEngine.Run(repoRoot, skillName, cliArgs.HasFlag("--fail-fast"), cliArgs.GetOption("--filter"));
        var format = (cliArgs.GetOption("--format") ?? "text").ToLowerInvariant();
        var outputPath = cliArgs.GetOption("--output");

        if (format == "junit")
        {
            var xml = SkillTestEngine.ToJunitXml(suite);
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                EnsureParentDirectory(outputPath);
                File.WriteAllText(outputPath, xml);
            }
            else
            {
                Console.WriteLine(xml);
            }

            return suite.Passed ? 0 : 1;
        }

        return WriteOutput(suite, cliArgs, textWriter: WriteSkillTestSummary, failureExitCode: suite.Passed ? 0 : 1);
    }

    private static int RunScaffold(string repoRoot, CliArguments cliArgs)
    {
        if (cliArgs.Positionals.Count == 0 || cliArgs.Positionals[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            var templates = ScaffoldEngine.LoadTemplates(repoRoot);
            return WriteOutput(templates, cliArgs, textWriter: WriteTemplateSummary);
        }

        var templateId = cliArgs.Positionals[0];
        var destination = cliArgs.Positionals.Count > 1
            ? cliArgs.Positionals[1]
            : Path.Combine(repoRoot, $"{templateId}-sample");
        var solutionName = cliArgs.GetOption("--name")
                           ?? ToSolutionName(Path.GetFileName(destination) ?? templateId);
        var dryRun = cliArgs.HasFlag("--dry-run");

        if (dryRun)
        {
            var plan = ScaffoldEngine.Plan(repoRoot, templateId, destination, solutionName);
            return WriteOutput(plan, cliArgs, textWriter: WriteScaffoldPlanSummary);
        }

        var executed = ScaffoldEngine.Execute(repoRoot, templateId, destination, solutionName, cliArgs.HasFlag("--force"));
        return WriteOutput(executed, cliArgs, textWriter: WriteScaffoldPlanSummary);
    }

    private static RepositoryProfile LoadOrAnalyzeProfile(string repoRoot, CliArguments cliArgs, DotNetEnvironmentReport? environment = null)
    {
        var profilePath = cliArgs.GetOption("--profile");
        if (!string.IsNullOrWhiteSpace(profilePath))
        {
            return JsonSerializer.Deserialize<RepositoryProfile>(File.ReadAllText(profilePath), JsonOptions)
                   ?? throw new InvalidOperationException($"Failed to deserialize repository profile from '{profilePath}'.");
        }

        return ProjectAnalyzer.Analyze(repoRoot, environment);
    }

    private static int WriteOutput<T>(T payload, CliArguments cliArgs, Action<T> textWriter, int? failureExitCode = null)
    {
        var format = (cliArgs.GetOption("--format") ?? "text").ToLowerInvariant();
        var outputPath = cliArgs.GetOption("--output");

        if (format == "json")
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                EnsureParentDirectory(outputPath);
                File.WriteAllText(outputPath, json);
                Console.WriteLine(outputPath);
            }
            else
            {
                Console.WriteLine(json);
            }

            return failureExitCode ?? 0;
        }

        textWriter(payload);

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            EnsureParentDirectory(outputPath);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, JsonOptions));
        }

        return failureExitCode ?? 0;
    }

    private static void WriteProfileSummary(RepositoryProfile profile)
    {
        Console.WriteLine($"Repo: {profile.RepoRoot}");
        Console.WriteLine($"Solutions: {profile.Solutions.Count}");
        Console.WriteLine($"Projects: {profile.Projects.Count}");
        Console.WriteLine($"Dominant kind: {profile.DominantProjectKind}");
        Console.WriteLine($"Target frameworks: {string.Join(", ", profile.TargetFrameworks)}");
        Console.WriteLine($"Technologies: {string.Join(", ", profile.Technologies)}");
        Console.WriteLine($"CI: {string.Join(", ", profile.CiProviders)}");
    }

    private static void WriteRecommendationSummary(RecommendationBundle bundle)
    {
        Console.WriteLine($"Repo kind: {bundle.Profile.DominantProjectKind}");
        Console.WriteLine($"Technologies: {string.Join(", ", bundle.Profile.Technologies)}");
        Console.WriteLine("Skills:");
        foreach (var item in bundle.Skills)
        {
            Console.WriteLine($"  {item.Id} ({item.Score}) - {string.Join("; ", item.Reasons)}");
        }

        Console.WriteLine("Subagents:");
        foreach (var item in bundle.Subagents)
        {
            Console.WriteLine($"  {item.Id} ({item.Score}) - {string.Join("; ", item.Reasons)}");
        }

        Console.WriteLine("Commands:");
        foreach (var item in bundle.Commands)
        {
            Console.WriteLine($"  {item.Id} ({item.Score}) - {string.Join("; ", item.Reasons)}");
        }
    }

    private static void WriteInitSummary(InitReport report)
    {
        WriteProfileSummary(report.Profile);
        Console.WriteLine();
        WriteRecommendationSummary(report.Recommendations);
        Console.WriteLine();
        WriteDoctorSummary(report.Doctor);
    }

    private static void WriteDoctorSummary(DoctorReport report)
    {
        Console.WriteLine($"Doctor status: {(report.Passed ? "pass" : "issues-found")}");
        foreach (var finding in report.Findings)
        {
            Console.WriteLine($"  [{finding.Severity}] {finding.Code}: {finding.Message}");
        }
    }

    private static void WriteValidationSummary(ValidationReport report)
    {
        Console.WriteLine($"Validation mode: {report.Mode}");
        if (!string.IsNullOrWhiteSpace(report.Target))
        {
            Console.WriteLine($"Target: {report.Target}");
        }

        foreach (var check in report.Checks)
        {
            var severity = string.IsNullOrWhiteSpace(check.Severity) ? "info" : check.Severity;
            Console.WriteLine($"  {(check.Passed ? "PASS" : "FAIL")} [{severity}] {check.Name}: {check.Message}");
            if (!string.IsNullOrWhiteSpace(check.Command))
            {
                Console.WriteLine($"    {check.Command}");
            }

            if (!string.IsNullOrWhiteSpace(check.Evidence))
            {
                foreach (var line in check.Evidence.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    Console.WriteLine($"    {line}");
                }
            }

            if (!string.IsNullOrWhiteSpace(check.Remediation))
            {
                var remediationLines = check.Remediation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (remediationLines.Length > 0)
                {
                    Console.WriteLine($"    remediation: {remediationLines[0]}");
                    for (var index = 1; index < remediationLines.Length; index++)
                    {
                        Console.WriteLine($"    {remediationLines[index]}");
                    }
                }
            }
        }
    }

    private static void WriteSearchSummary(IReadOnlyList<CatalogSearchResult> results)
    {
        foreach (var result in results)
        {
            Console.WriteLine($"{result.Item.Id} [{result.Item.Kind}] score={result.Score}");
            Console.WriteLine($"  {result.Item.Description}");
            if (result.Reasons.Count > 0)
            {
                Console.WriteLine($"  reasons: {string.Join("; ", result.Reasons)}");
            }
        }
    }

    private static void WriteReviewSummary(ReviewReport report)
    {
        Console.WriteLine($"Scanned files: {report.ScannedFiles}");
        Console.WriteLine($"Findings: {report.Findings.Count}");
        foreach (var finding in report.Findings)
        {
            Console.WriteLine($"  [{finding.Severity}] {finding.RuleId} {finding.FilePath}:{finding.LineNumber}");
            Console.WriteLine($"    {finding.Message}");
            Console.WriteLine($"    {finding.Evidence}");
        }
    }

    private static void WritePreparedMessageSummary(PreparedMessageReport report)
    {
        Console.WriteLine($"Persona: {report.Persona.Id}");
        Console.WriteLine($"Purpose: {report.Persona.Purpose}");
        Console.WriteLine($"Platform: {report.RenderedPrompt.Platform}");
        Console.WriteLine($"Target: {(string.IsNullOrWhiteSpace(report.Target.DisplayPath) ? "unresolved" : report.Target.DisplayPath)}");
        Console.WriteLine($"Resolution: {report.Target.Resolution}");
        if (report.Subagent is not null)
        {
            Console.WriteLine($"Subagent: {report.Subagent.Id}");
        }
        if (report.Evidence is not null)
        {
            Console.WriteLine($"Evidence ID: {report.Evidence.EvidenceId}");
            Console.WriteLine($"Evidence report: {report.Evidence.ReportPath}");
            Console.WriteLine($"Evidence prompt: {report.Evidence.PromptPath}");
        }

        Console.WriteLine("Skills:");
        foreach (var skill in report.Skills)
        {
            Console.WriteLine($"  {skill.Id} [{skill.Source}] - {string.Join("; ", skill.Reasons)}");
        }

        if (report.Risks.Count > 0)
        {
            Console.WriteLine("Risks:");
            foreach (var risk in report.Risks)
            {
                Console.WriteLine($"  {risk}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Enhanced request:");
        foreach (var line in report.EnhancedRequest.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            Console.WriteLine($"  {line}");
        }

        Console.WriteLine();
        Console.WriteLine("Rendered prompt:");
        foreach (var line in report.RenderedPrompt.CompositeText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            Console.WriteLine($"  {line}");
        }
    }

    private static void WritePromptComparisonSummary(PromptComparisonReport report)
    {
        Console.WriteLine($"Left: {report.LeftEvidenceId} ({report.LeftPersonaId}, {report.LeftPlatform})");
        Console.WriteLine($"Right: {report.RightEvidenceId} ({report.RightPersonaId}, {report.RightPlatform})");
        Console.WriteLine($"Targets: {report.LeftTarget} / {report.RightTarget}");
        Console.WriteLine($"Changed sections: {(report.ChangedSections.Count == 0 ? "none" : string.Join(", ", report.ChangedSections))}");

        foreach (var section in report.Sections)
        {
            Console.WriteLine($"  {section.SectionName}: {(section.IsIdentical ? "same" : "changed")}");
            Console.WriteLine($"    lines: {section.LeftLineCount} / {section.RightLineCount}");
            if (section.FirstDifferenceLine is not null)
            {
                Console.WriteLine($"    first difference: line {section.FirstDifferenceLine}");
            }

            foreach (var line in section.LeftOnlyLines)
            {
                Console.WriteLine($"    left-only: {line}");
            }

            foreach (var line in section.RightOnlyLines)
            {
                Console.WriteLine($"    right-only: {line}");
            }
        }
    }

    private static void WriteIncidentSummary(PromptIncidentRecord record)
    {
        Console.WriteLine($"Incident: {record.IncidentId}");
        Console.WriteLine($"Title: {record.Title}");
        Console.WriteLine($"Status: {record.Status}");
        Console.WriteLine($"Severity: {record.Severity}");
        Console.WriteLine($"Persona: {record.PersonaId}");
        Console.WriteLine($"Platform: {record.Platform}");
        Console.WriteLine($"Target: {record.Target}");
        Console.WriteLine($"Prompt evidence: {record.PromptEvidence.EvidenceId}");
        Console.WriteLine($"Incident file: {record.FilePath}");
        if (!string.IsNullOrWhiteSpace(record.Owner))
        {
            Console.WriteLine($"Owner: {record.Owner}");
        }

        if (!string.IsNullOrWhiteSpace(record.Notes))
        {
            Console.WriteLine($"Notes: {record.Notes}");
        }

        if (record.EvalContext is not null)
        {
            Console.WriteLine($"Eval artifact: {record.EvalContext.ArtifactPath}");
            Console.WriteLine($"Eval run: {record.EvalContext.RunId} ({record.EvalContext.Gate}/{record.EvalContext.PolicyProfile})");
            Console.WriteLine($"Eval pass rate: {record.EvalContext.PassRate:P1} with {record.EvalContext.FailedTrials} failed trial(s)");
        }

        if (record.Resolution is not null)
        {
            Console.WriteLine($"Resolved by: {record.Resolution.Owner} at {record.Resolution.ResolvedAtUtc:O}");
            Console.WriteLine($"Resolution rationale: {record.Resolution.Rationale}");
            Console.WriteLine($"Regression case: {record.Resolution.RegressionCaseId}");
            if (!string.IsNullOrWhiteSpace(record.Resolution.RegressionCasePath))
            {
                Console.WriteLine($"Regression path: {record.Resolution.RegressionCasePath}");
            }

            if (!string.IsNullOrWhiteSpace(record.Resolution.Notes))
            {
                Console.WriteLine($"Resolution notes: {record.Resolution.Notes}");
            }
        }
    }

    private static void WriteIncidentListSummary(IReadOnlyList<PromptIncidentRecord> incidents)
    {
        Console.WriteLine($"Incidents: {incidents.Count}");
        foreach (var incident in incidents)
        {
            Console.WriteLine($"  {incident.IncidentId} [{incident.Status}/{incident.Severity}] {incident.Title}");
            Console.WriteLine($"    persona={incident.PersonaId} platform={incident.Platform} prompt={incident.PromptEvidence.EvidenceId}");
            if (incident.EvalContext is not null)
            {
                Console.WriteLine($"    eval={incident.EvalContext.RunId} failed={incident.EvalContext.FailedTrials} pass_rate={incident.EvalContext.PassRate:P1}");
            }

            if (incident.Resolution is not null)
            {
                Console.WriteLine($"    resolution={incident.Resolution.Status} by={incident.Resolution.Owner} regression={incident.Resolution.RegressionCaseId}");
            }
        }
    }

    private static void WriteSkillTestSummary(SkillTestSuiteResult suite)
    {
        Console.WriteLine($"Skills: {suite.Skills.Count}");
        Console.WriteLine($"Cases: {suite.TotalCases}");
        Console.WriteLine($"Skills with cases: {suite.SkillsWithCases}");
        Console.WriteLine($"Skills without cases: {suite.SkillsWithoutCases}");
        Console.WriteLine($"Checks: {suite.TotalChecks}");
        Console.WriteLine($"Failures: {suite.FailedChecks}");
        foreach (var skill in suite.Skills)
        {
            Console.WriteLine($"{skill.SkillId}: {(skill.Passed ? "PASS" : "FAIL")} ({skill.CaseCount} case(s))");
            foreach (var check in skill.Checks)
            {
                Console.WriteLine($"  {(check.Passed ? "PASS" : "FAIL")} {check.Name} - {check.Message}");
            }
        }
    }

    private static void WriteTemplateSummary(IReadOnlyList<ScaffoldTemplate> templates)
    {
        foreach (var template in templates)
        {
            Console.WriteLine($"{template.Id}: {template.Description}");
        }
    }

    private static void WriteScaffoldPlanSummary(ScaffoldPlan plan)
    {
        Console.WriteLine($"Template: {plan.TemplateId}");
        Console.WriteLine($"Destination: {plan.Destination}");
        Console.WriteLine($"Solution: {plan.SolutionName}");
        foreach (var step in plan.Steps)
        {
            Console.WriteLine($"  [{step.Kind}] {step.Description}");
            Console.WriteLine($"    {step.Command}");
        }
    }

    private static string ResolveRepoRoot(CliArguments cliArgs)
    {
        var repoRoot = cliArgs.GetOption("--repo") ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(repoRoot);
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static int PrintHelpAndReturn()
    {
        PrintUsage();
        return 0;
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static PromptIncidentResolutionOptions BuildIncidentResolutionOptions(CliArguments cliArgs)
    {
        return new PromptIncidentResolutionOptions
        {
            Owner = cliArgs.GetOption("--owner") ?? string.Empty,
            Rationale = cliArgs.GetOption("--rationale") ?? string.Empty,
            RegressionCaseId = cliArgs.GetOption("--regression-case") ?? string.Empty,
            RegressionCasePath = cliArgs.GetOption("--regression-path") ?? string.Empty,
            Notes = cliArgs.GetOption("--notes") ?? string.Empty
        };
    }

    private static string ToSolutionName(string raw)
    {
        var chars = raw.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "Sample" : new string(chars);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("dotnet-agent-harness tools");
        Console.WriteLine("Commands:");
        Console.WriteLine("  lint-frontmatter");
        Console.WriteLine("  build-manifest [--output path]");
        Console.WriteLine("  build-catalog [--output path]");
        Console.WriteLine("  analyze [--format text|json] [--write-state]");
        Console.WriteLine("  recommend [--format text|json] [--limit N] [--profile path] [--write-state]");
        Console.WriteLine("  init [--format text|json] [--limit N] [--no-save]");
        Console.WriteLine("  doctor [--format text|json] [--profile path] [--write-state]");
        Console.WriteLine("  validate [--mode repo|skill|eval|all] [--run] [--target path] [--configuration Debug|Release] [--framework tfm] [--timeout-ms N] [--skip-restore] [--skip-build] [--skip-test] [--format text|json]");
        Console.WriteLine("  search <query> [--kind skill|subagent|command|persona] [--category value] [--platform value] [--limit N]");
        Console.WriteLine("  profile [catalog-item-id] [--format text|json]");
        Console.WriteLine("  compare <left-id> <right-id> [--format text|json]");
        Console.WriteLine("  compare-prompts <left-evidence-id> <right-evidence-id> [--format text|json]");
        Console.WriteLine("  prepare-message <request> [--persona id] [--target path] [--platform generic|codexcli|claudecode|opencode|copilot] [--limit N] [--write-evidence] [--evidence-id id] [--format text|json|prompt]");
        Console.WriteLine("  incident add <title> --prompt-evidence id [--incident-id id] [--severity low|medium|high|critical] [--owner name] [--notes text] [--format text|json]");
        Console.WriteLine("  incident list [--format text|json]");
        Console.WriteLine("  incident show <incident-id> [--format text|json]");
        Console.WriteLine("  incident from-eval <artifact-path-or-id> [--prompt-evidence id] [--incident-id id] [--severity low|medium|high|critical] [--owner name] [--notes text] [--format text|json]");
        Console.WriteLine("  incident resolve <incident-id> --owner name --rationale text --regression-case id [--regression-path path] [--notes text] [--format text|json]");
        Console.WriteLine("  incident close <incident-id> --owner name --rationale text --regression-case id [--regression-path path] [--notes text] [--format text|json]");
        Console.WriteLine("  review [path] [--format text|json] [--limit N]");
        Console.WriteLine("  test [skill-name|all] [--format text|json|junit] [--filter value] [--fail-fast]");
        Console.WriteLine("  scaffold [list|template] [destination] [--name SolutionName] [--dry-run] [--force]");
    }
}
