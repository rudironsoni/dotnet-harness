using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DotNetAgentHarness.Tools.Engine;

public static class RepoValidationEngine
{
    private static readonly Regex DiagnosticCodeRegex = new(@"\b(?:CS|MSB|NU|CA|IDE|NETSDK)\d{4}\b", RegexOptions.Compiled);

    public static IReadOnlyList<ValidationCheck> Validate(string repoRoot, RepositoryProfile profile, ValidationOptions options, DotNetEnvironmentReport? environment = null)
    {
        var checks = new List<ValidationCheck>();
        if (!options.RunDotNet)
        {
            return checks;
        }

        environment ??= ProjectAnalyzer.ProbeDotNetEnvironment();
        if (!environment.IsAvailable)
        {
            checks.Add(new ValidationCheck
            {
                Name = "dotnet-runtime",
                Passed = false,
                Severity = "error",
                Message = string.IsNullOrWhiteSpace(environment.ErrorMessage)
                    ? ".NET SDK is not available, so runtime validation could not start."
                    : $".NET SDK is not available: {environment.ErrorMessage}",
                Remediation = "Install the .NET SDK and verify `dotnet --list-sdks` succeeds before running `validate --run`."
            });
            return checks;
        }

        var selection = RepoTargetResolver.Resolve(repoRoot, profile, options.TargetPath);
        var targetCheck = BuildTargetCheck(selection);
        checks.Add(targetCheck);
        if (!targetCheck.Passed || string.IsNullOrWhiteSpace(selection.TargetPath))
        {
            return checks;
        }

        var target = selection.TargetPath;
        var restoreSucceeded = false;
        var buildSucceeded = false;

        if (!options.SkipRestore)
        {
            var restoreCheck = RunDotNetCommand(
                "dotnet-restore",
                BuildDotNetArguments("restore", target, options, includeNoRestore: false, includeNoBuild: false),
                repoRoot,
                options.TimeoutMs);
            checks.Add(restoreCheck);
            restoreSucceeded = restoreCheck.Passed;
            if (!restoreCheck.Passed)
            {
                return checks;
            }
        }

        if (!options.SkipBuild)
        {
            var buildCheck = RunDotNetCommand(
                "dotnet-build",
                BuildDotNetArguments("build", target, options, includeNoRestore: restoreSucceeded, includeNoBuild: false),
                repoRoot,
                options.TimeoutMs);
            checks.Add(buildCheck);
            buildSucceeded = buildCheck.Passed;
            if (!buildCheck.Passed)
            {
                return checks;
            }
        }

        if (options.SkipTest)
        {
            checks.Add(new ValidationCheck
            {
                Name = "dotnet-test-skipped",
                Passed = true,
                Severity = "info",
                Message = "Test execution was skipped by option.",
                Remediation = "Run `validate --run` without `--skip-test` when you want end-to-end repo verification."
            });
            return checks;
        }

        if (profile.TestProjectCount == 0)
        {
            checks.Add(new ValidationCheck
            {
                Name = "dotnet-test-skipped",
                Passed = true,
                Severity = "info",
                Message = "No test projects were detected, so `dotnet test` was skipped.",
                Remediation = "Add a dedicated test project if you want the harness to verify behavior with repo-native tests."
            });
            return checks;
        }

        var canRunTestsAgainstTarget = CanRunTestsAgainstTarget(target, profile);
        if (!canRunTestsAgainstTarget)
        {
            checks.Add(new ValidationCheck
            {
                Name = "dotnet-test-skipped",
                Passed = false,
                Severity = "warning",
                Message = $"Target '{selection.DisplayPath}' is not a solution or test project, so `dotnet test` would not cover the repository tests.",
                Remediation = "Pass `--target <solution-or-test-project>` or add a solution file so runtime validation can execute repo-native tests."
            });
            return checks;
        }

        var testCheck = RunDotNetCommand(
            "dotnet-test",
            BuildDotNetArguments("test", target, options, includeNoRestore: restoreSucceeded, includeNoBuild: buildSucceeded),
            repoRoot,
            options.TimeoutMs);
        checks.Add(testCheck);

        return checks;
    }

    private static ValidationCheck RunDotNetCommand(string name, string arguments, string workingDirectory, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = ProcessRunner.Run("dotnet", arguments, workingDirectory, timeoutMs, throwOnError: false);
        stopwatch.Stop();

        var output = CombineOutput(result);
        var diagnosticCodes = DiagnosticCodeRegex.Matches(output)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        var remediation = ResolveRemediation(diagnosticCodes.FirstOrDefault(), name);
        var evidence = ExtractEvidence(output);

        return new ValidationCheck
        {
            Name = name,
            Passed = !result.TimedOut && result.ExitCode == 0,
            Severity = !result.TimedOut && result.ExitCode == 0 ? "info" : "error",
            Message = BuildMessage(name, result, stopwatch.Elapsed.TotalMilliseconds, diagnosticCodes),
            Command = $"dotnet {arguments}",
            ExitCode = result.ExitCode,
            DurationMs = stopwatch.Elapsed.TotalMilliseconds,
            Evidence = evidence,
            Remediation = !result.TimedOut && result.ExitCode == 0 ? string.Empty : remediation
        };
    }

    private static string BuildDotNetArguments(string verb, string targetPath, ValidationOptions options, bool includeNoRestore, bool includeNoBuild)
    {
        var builder = new StringBuilder();
        builder.Append(verb).Append(' ').Append(Quote(targetPath)).Append(" --nologo");
        if (!verb.Equals("restore", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.Configuration))
        {
            builder.Append(" --configuration ").Append(Quote(options.Configuration));
        }

        if (!string.IsNullOrWhiteSpace(options.Framework))
        {
            builder.Append(" --framework ").Append(Quote(options.Framework!));
        }

        if (includeNoRestore)
        {
            builder.Append(" --no-restore");
        }

        if (includeNoBuild && verb.Equals("test", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(" --no-build");
        }

        return builder.ToString();
    }

    private static ValidationCheck BuildTargetCheck(RepoTargetSelection selection)
    {
        if (!string.IsNullOrWhiteSpace(selection.TargetPath))
        {
            return new ValidationCheck
            {
                Name = "dotnet-target",
                Passed = true,
                Severity = "info",
                Message = $"Runtime validation target: {selection.DisplayPath}"
            };
        }

        if (selection.IsExplicit)
        {
            return new ValidationCheck
            {
                Name = "dotnet-target",
                Passed = false,
                Severity = "error",
                Message = $"Requested target '{selection.DisplayPath}' was not found.",
                Remediation = "Pass `--target` with a valid solution or project path relative to the repository root."
            };
        }

        if (selection.Candidates.Count > 1)
        {
            return new ValidationCheck
            {
                Name = "dotnet-target",
                Passed = false,
                Severity = "warning",
                Message = $"Multiple candidate projects were detected ({string.Join(", ", selection.Candidates)}). Runtime validation needs an explicit `--target` or a solution file.",
                Remediation = "Add a solution file or pass `--target <project-or-solution>` so the harness validates the intended scope."
            };
        }

        return new ValidationCheck
        {
            Name = "dotnet-target",
            Passed = false,
            Severity = "warning",
            Message = selection.Resolution,
            Remediation = "Pass `--target <project-or-solution>` so the harness can validate a concrete repository entry point."
        };
    }

    private static bool CanRunTestsAgainstTarget(string targetPath, RepositoryProfile profile)
    {
        if (targetPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            || targetPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var relativePath = Path.GetRelativePath(profile.RepoRoot, targetPath);
        var project = profile.Projects.FirstOrDefault(item => item.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        return project?.IsTestProject == true;
    }

    private static string BuildMessage(string commandName, ProcessExecutionResult result, double durationMs, IReadOnlyList<string> diagnosticCodes)
    {
        if (result.TimedOut)
        {
            return $"{commandName} timed out after {durationMs:F0}ms.";
        }

        if (result.ExitCode == 0)
        {
            return $"{commandName} succeeded in {durationMs:F0}ms.";
        }

        return diagnosticCodes.Count > 0
            ? $"{commandName} failed with exit code {result.ExitCode}. Diagnostic codes: {string.Join(", ", diagnosticCodes)}."
            : $"{commandName} failed with exit code {result.ExitCode}.";
    }

    private static string ResolveRemediation(string? diagnosticCode, string commandName)
    {
        if (string.IsNullOrWhiteSpace(diagnosticCode))
        {
            return commandName switch
            {
                "dotnet-restore" => "Inspect NuGet sources, package references, and network/feed access, then rerun the restore command directly.",
                "dotnet-build" => "Inspect the first compiler or MSBuild error in the command output, fix the underlying project or source issue, then rerun build.",
                "dotnet-test" => "Inspect the failing test output, reproduce locally with `dotnet test`, and fix either the implementation or the test setup.",
                _ => "Inspect the command output and rerun the failing command directly."
            };
        }

        if (diagnosticCode.StartsWith("CS", StringComparison.OrdinalIgnoreCase))
        {
            return "Fix the compiler error at the reported file and line, then rerun build. Missing usings, types, references, or syntax errors usually cause CS diagnostics.";
        }

        if (diagnosticCode.StartsWith("MSB", StringComparison.OrdinalIgnoreCase))
        {
            return "Inspect project SDK, imports, workloads, and target frameworks. MSBuild diagnostics usually point to project configuration or SDK issues.";
        }

        if (diagnosticCode.StartsWith("NU", StringComparison.OrdinalIgnoreCase))
        {
            return "Inspect package IDs, NuGet sources, and version conflicts. NU diagnostics usually indicate feed, package, or dependency resolution problems.";
        }

        if (diagnosticCode.StartsWith("NETSDK", StringComparison.OrdinalIgnoreCase))
        {
            return "Align installed SDKs, workloads, and `global.json` with the project target frameworks, then rerun the command.";
        }

        if (diagnosticCode.StartsWith("CA", StringComparison.OrdinalIgnoreCase)
            || diagnosticCode.StartsWith("IDE", StringComparison.OrdinalIgnoreCase))
        {
            return "Apply the analyzer recommendation or adjust analyzer severity intentionally in `.editorconfig` or shared build props.";
        }

        return "Inspect the failing command output and fix the first reported diagnostic before rerunning validation.";
    }

    private static string CombineOutput(ProcessExecutionResult result)
    {
        return string.Join(
            Environment.NewLine,
            new[] { result.StandardOutput, result.StandardError }
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text.Trim()));
    }

    private static string ExtractEvidence(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return string.Empty;
        }

        var lines = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains("error", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("warning", StringComparison.OrdinalIgnoreCase)
                        || DiagnosticCodeRegex.IsMatch(line))
            .Take(8)
            .ToList();

        if (lines.Count == 0)
        {
            lines = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(8)
                .ToList();
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }
}
