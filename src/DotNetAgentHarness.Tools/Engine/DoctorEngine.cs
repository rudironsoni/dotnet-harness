using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNetAgentHarness.Tools.Engine;

public static class DoctorEngine
{
    public static DoctorReport BuildReport(RepositoryProfile profile, DotNetEnvironmentReport? environment = null)
    {
        environment ??= new DotNetEnvironmentReport
        {
            IsAvailable = profile.InstalledSdkVersions.Count > 0,
            InstalledSdkVersions = profile.InstalledSdkVersions
        };

        var findings = new List<DoctorFinding>();

        if (!environment.IsAvailable)
        {
            findings.Add(new DoctorFinding
            {
                Severity = "error",
                Code = "dotnet-missing",
                Message = string.IsNullOrWhiteSpace(environment.ErrorMessage)
                    ? ".NET SDK is not available on PATH."
                    : $".NET SDK probe failed: {environment.ErrorMessage}",
                Remediation = "Install the .NET SDK and verify `dotnet --list-sdks` succeeds."
            });
        }

        if (profile.Projects.Count == 0 && profile.Solutions.Count == 0)
        {
            findings.Add(new DoctorFinding
            {
                Severity = "error",
                Code = "repo-no-dotnet-projects",
                Message = "No .sln, .slnx, or .csproj files were found in the repository.",
                Remediation = "Run the harness inside a .NET repository or scaffold a project before using repo-aware commands."
            });
        }

        foreach (var loadError in profile.LoadErrors)
        {
            findings.Add(new DoctorFinding
            {
                Severity = "warning",
                Code = "project-load-error",
                Message = $"Project analysis skipped a file: {loadError}",
                Remediation = "Fix invalid project XML so repo analysis and recommendations can cover the full codebase."
            });
        }

        if (profile.Projects.Count > 1 && profile.Solutions.Count == 0)
        {
            findings.Add(new DoctorFinding
            {
                Severity = "warning",
                Code = "solution-missing",
                Message = "Multiple .csproj files were detected without a solution file.",
                Remediation = "Create a solution file to make repo-wide builds, tests, and agent navigation more predictable."
            });
        }

        if (profile.Projects.Count > 1 && !profile.HasDirectoryBuildProps)
        {
            findings.Add(new DoctorFinding
            {
                Severity = "warning",
                Code = "directory-build-props-missing",
                Message = "The repository has multiple projects but no root Directory.Build.props.",
                Remediation = "Add Directory.Build.props to centralize shared .NET properties and analyzer configuration."
            });
        }

        if (!profile.HasEditorConfig)
        {
            findings.Add(new DoctorFinding
            {
                Severity = "warning",
                Code = "editorconfig-missing",
                Message = "No root .editorconfig file was found.",
                Remediation = "Add a root .editorconfig so agents and humans share the same style and analyzer expectations."
            });
        }

        if (!profile.HasDotNetToolManifest)
        {
            findings.Add(new DoctorFinding
            {
                Severity = "info",
                Code = "tool-manifest-missing",
                Message = "No local .NET tool manifest was found.",
                Remediation = "Run `dotnet new tool-manifest` and pin repo-local tools for reproducible agent workflows."
            });
        }

        if (!profile.HasRulesync)
        {
            findings.Add(new DoctorFinding
            {
                Severity = "info",
                Code = "rulesync-missing",
                Message = "RuleSync source files are not present in this repository.",
                Remediation = "Install the toolkit source with RuleSync if you want generated agent rules, commands, and hooks in this repo."
            });
        }

        if (profile.TestProjectCount == 0 && profile.Projects.Any(project => project.Kind != "test"))
        {
            findings.Add(new DoctorFinding
            {
                Severity = "warning",
                Code = "tests-missing",
                Message = "No dedicated test project was detected.",
                Remediation = "Add a test project so agents can validate changes against repo-native tests instead of relying on reasoning alone."
            });
        }

        if (profile.CiProviders.Count == 0)
        {
            findings.Add(new DoctorFinding
            {
                Severity = "info",
                Code = "ci-missing",
                Message = "No GitHub Actions or Azure DevOps pipeline definition was detected.",
                Remediation = "Add CI so the harness can validate repo changes consistently before promotion."
            });
        }

        var missingTfms = ResolveMissingFrameworkSupport(profile.TargetFrameworks, environment.InstalledSdkVersions);
        foreach (var missingTfm in missingTfms)
        {
            findings.Add(new DoctorFinding
            {
                Severity = "warning",
                Code = "sdk-version-gap",
                Message = $"No installed SDK appears to match target framework '{missingTfm}'.",
                Remediation = "Install the matching SDK or update global.json to a version that supports the target framework."
            });
        }

        return new DoctorReport
        {
            RepoRoot = profile.RepoRoot,
            Findings = findings
                .OrderBy(finding => SeverityRank(finding.Severity))
                .ThenBy(finding => finding.Code, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static IEnumerable<string> ResolveMissingFrameworkSupport(IEnumerable<string> targetFrameworks, IEnumerable<string> installedSdkVersions)
    {
        var installedMajors = installedSdkVersions
            .Select(version => version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault())
            .Where(major => !string.IsNullOrWhiteSpace(major))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tfm in targetFrameworks)
        {
            if (!tfm.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var major = new string(tfm.Skip(3).TakeWhile(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(major))
            {
                continue;
            }

            if (!installedMajors.Contains(major))
            {
                yield return tfm;
            }
        }
    }

    private static int SeverityRank(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "error" => 0,
            "warning" => 1,
            _ => 2
        };
    }
}
