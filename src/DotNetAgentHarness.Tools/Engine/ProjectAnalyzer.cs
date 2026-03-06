using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace DotNetAgentHarness.Tools.Engine;

public static class ProjectAnalyzer
{
    private static readonly string[] IgnoredDirectoryNames = { ".git", ".tmp", "bin", "obj", "node_modules", "dist", ".rulesync", ".codex", ".claude", ".opencode" };
    private static readonly string[] TestPackages = { "xunit", "nunit", "mstest", "microsoft.net.test.sdk" };

    public static RepositoryProfile Analyze(string repoRoot, DotNetEnvironmentReport? environment = null)
    {
        var normalizedRoot = Path.GetFullPath(repoRoot);
        environment ??= ProbeDotNetEnvironment();

        var solutionFiles = EnumerateFiles(normalizedRoot, filePath =>
            filePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            || filePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(normalizedRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var projectFiles = EnumerateFiles(normalizedRoot, filePath =>
                filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var projects = new List<ProjectSummary>();
        var loadErrors = new List<string>();
        foreach (var projectFile in projectFiles)
        {
            try
            {
                projects.Add(AnalyzeProject(normalizedRoot, projectFile));
            }
            catch (Exception ex)
            {
                loadErrors.Add($"{Path.GetRelativePath(normalizedRoot, projectFile)}: {ex.Message}");
            }
        }

        var packageIds = projects
            .SelectMany(project => project.PackageIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var targetFrameworks = projects
            .SelectMany(project => project.TargetFrameworks)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tfm => tfm, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var technologies = DetectTechnologies(projects, packageIds, normalizedRoot);
        var projectKinds = projects
            .Where(project => !string.IsNullOrWhiteSpace(project.Kind))
            .Select(project => project.Kind)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dominantProjectKind = ResolveDominantProjectKind(projects);
        var dominantTestFramework = ResolveDominantTestFramework(packageIds);

        return new RepositoryProfile
        {
            RepoRoot = normalizedRoot,
            Solutions = solutionFiles,
            Projects = projects,
            GlobalJsonSdkVersion = ReadGlobalJsonSdkVersion(normalizedRoot),
            InstalledSdkVersions = environment.InstalledSdkVersions,
            HasDirectoryBuildProps = File.Exists(Path.Combine(normalizedRoot, "Directory.Build.props")),
            HasEditorConfig = File.Exists(Path.Combine(normalizedRoot, ".editorconfig")),
            HasDotNetToolManifest = File.Exists(Path.Combine(normalizedRoot, ".config", "dotnet-tools.json")),
            HasRulesync = Directory.Exists(Path.Combine(normalizedRoot, ".rulesync")),
            CiProviders = DetectCiProviders(normalizedRoot),
            TargetFrameworks = targetFrameworks,
            Technologies = technologies,
            ProjectKinds = projectKinds,
            DominantProjectKind = dominantProjectKind,
            DominantTestFramework = dominantTestFramework,
            TestProjectCount = projects.Count(project => project.IsTestProject),
            PackageIds = packageIds,
            LoadErrors = loadErrors
        };
    }

    public static DotNetEnvironmentReport ProbeDotNetEnvironment()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-sdks",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                return new DotNetEnvironmentReport
                {
                    IsAvailable = false,
                    ErrorMessage = "Failed to start dotnet process."
                };
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
            {
                return new DotNetEnvironmentReport
                {
                    IsAvailable = false,
                    ErrorMessage = string.IsNullOrWhiteSpace(error) ? "dotnet returned a non-zero exit code." : error.Trim()
                };
            }

            var versions = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new DotNetEnvironmentReport
            {
                IsAvailable = true,
                InstalledSdkVersions = versions
            };
        }
        catch (Exception ex)
        {
            return new DotNetEnvironmentReport
            {
                IsAvailable = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static ProjectSummary AnalyzeProject(string repoRoot, string projectFile)
    {
        var document = XDocument.Load(projectFile);
        var projectElement = document.Root ?? throw new InvalidDataException($"Project file '{projectFile}' is missing a root element.");
        var sdk = projectElement.Attribute("Sdk")?.Value ?? string.Empty;
        var targetFrameworks = GetPropertyValues(document, "TargetFramework")
            .Concat(GetPropertyValues(document, "TargetFrameworks").SelectMany(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var outputType = GetPropertyValues(document, "OutputType").FirstOrDefault() ?? string.Empty;
        var useMaui = GetPropertyValues(document, "UseMaui").Any(value => value.Equals("true", StringComparison.OrdinalIgnoreCase));
        var packageIds = document.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var isTestProject = LooksLikeTestProject(projectFile)
                            || packageIds.Any(package => TestPackages.Contains(package, StringComparer.OrdinalIgnoreCase));

        return new ProjectSummary
        {
            Name = Path.GetFileNameWithoutExtension(projectFile),
            RelativePath = Path.GetRelativePath(repoRoot, projectFile),
            Sdk = sdk,
            TargetFrameworks = targetFrameworks,
            OutputType = outputType,
            UseMaui = useMaui,
            IsTestProject = isTestProject,
            Kind = DetectProjectKind(sdk, outputType, useMaui, packageIds, isTestProject),
            PackageIds = packageIds
        };
    }

    private static List<string> DetectTechnologies(IReadOnlyList<ProjectSummary> projects, IReadOnlyList<string> packageIds, string repoRoot)
    {
        var technologies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (projects.Any(project => project.Sdk.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add("aspnetcore");
        }

        if (packageIds.Any(package => package.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add("efcore");
        }

        if (projects.Any(project => project.Sdk.Contains("BlazorWebAssembly", StringComparison.OrdinalIgnoreCase))
            || packageIds.Any(package => package.Contains("AspNetCore.Components", StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add("blazor");
        }

        if (projects.Any(project => project.UseMaui) || packageIds.Any(package => package.StartsWith("Microsoft.Maui", StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add("maui");
        }

        if (packageIds.Any(package => package.StartsWith("Aspire.", StringComparison.OrdinalIgnoreCase) || package.Contains("Aspire.Hosting", StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add("aspire");
        }

        if (packageIds.Any(package => package.Contains("Grpc", StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add("grpc");
        }

        if (packageIds.Any(package => package.Contains("SignalR", StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add("signalr");
        }

        if (packageIds.Any(package => package.Contains("OpenApi", StringComparison.OrdinalIgnoreCase) || package.Contains("Swashbuckle", StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add("openapi");
        }

        if (packageIds.Any(package => package.Contains("Polly", StringComparison.OrdinalIgnoreCase)))
        {
            technologies.Add("resilience");
        }

        if (projects.Any(project => project.Kind == "console"))
        {
            technologies.Add("cli");
        }

        if (projects.Any(project => project.Kind == "test"))
        {
            technologies.Add("testing");
        }

        if (File.Exists(Path.Combine(repoRoot, "Dockerfile")) || File.Exists(Path.Combine(repoRoot, "docker-compose.yml")) || File.Exists(Path.Combine(repoRoot, "docker-compose.yaml")))
        {
            technologies.Add("containers");
        }

        if (Directory.Exists(Path.Combine(repoRoot, ".github", "workflows")))
        {
            technologies.Add("github-actions");
        }

        if (EnumerateFiles(repoRoot, path =>
                Path.GetFileName(path).StartsWith("azure-pipelines", StringComparison.OrdinalIgnoreCase)
                && (path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))).Any())
        {
            technologies.Add("azure-devops");
        }

        return technologies.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> DetectCiProviders(string repoRoot)
    {
        var providers = new List<string>();
        if (Directory.Exists(Path.Combine(repoRoot, ".github", "workflows")))
        {
            providers.Add("github-actions");
        }

        if (EnumerateFiles(repoRoot, path =>
                Path.GetFileName(path).StartsWith("azure-pipelines", StringComparison.OrdinalIgnoreCase)
                && (path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))).Any())
        {
            providers.Add("azure-devops");
        }

        return providers;
    }

    private static string ResolveDominantProjectKind(IEnumerable<ProjectSummary> projects)
    {
        return projects
            .Where(project => project.Kind != "test")
            .GroupBy(project => project.Kind, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault() ?? "unknown";
    }

    private static string ResolveDominantTestFramework(IEnumerable<string> packageIds)
    {
        if (packageIds.Any(package => package.Equals("xunit", StringComparison.OrdinalIgnoreCase)))
        {
            return "xunit";
        }

        if (packageIds.Any(package => package.Equals("nunit", StringComparison.OrdinalIgnoreCase)))
        {
            return "nunit";
        }

        if (packageIds.Any(package => package.Contains("MSTest", StringComparison.OrdinalIgnoreCase)))
        {
            return "mstest";
        }

        return "none";
    }

    private static string DetectProjectKind(string sdk, string outputType, bool useMaui, IReadOnlyList<string> packageIds, bool isTestProject)
    {
        if (isTestProject)
        {
            return "test";
        }

        if (useMaui)
        {
            return "maui";
        }

        if (sdk.Contains("BlazorWebAssembly", StringComparison.OrdinalIgnoreCase))
        {
            return "blazor";
        }

        if (packageIds.Any(package => package.Contains("AspNetCore.Components", StringComparison.OrdinalIgnoreCase)))
        {
            return "blazor";
        }

        if (sdk.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase))
        {
            return "web";
        }

        if (string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase))
        {
            return "console";
        }

        return "classlib";
    }

    private static string? ReadGlobalJsonSdkVersion(string repoRoot)
    {
        var globalJsonPath = Path.Combine(repoRoot, "global.json");
        if (!File.Exists(globalJsonPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
            if (document.RootElement.TryGetProperty("sdk", out var sdkElement)
                && sdkElement.TryGetProperty("version", out var versionElement))
            {
                return versionElement.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static IEnumerable<string> GetPropertyValues(XDocument document, string propertyName)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName == propertyName)
            .Select(element => element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }

    private static IEnumerable<string> EnumerateFiles(string root, Func<string, bool> predicate)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (ShouldIgnore(directory))
                {
                    continue;
                }

                pending.Push(directory);
            }

            foreach (var filePath in Directory.EnumerateFiles(current))
            {
                if (predicate(filePath))
                {
                    yield return filePath;
                }
            }
        }
    }

    private static bool ShouldIgnore(string path)
    {
        var directoryName = Path.GetFileName(path);
        return IgnoredDirectoryNames.Contains(directoryName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool LooksLikeTestProject(string projectFile)
    {
        var fileName = Path.GetFileNameWithoutExtension(projectFile);
        if (fileName.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parent = Directory.GetParent(projectFile);
        while (parent is not null)
        {
            if (parent.Name.Equals("tests", StringComparison.OrdinalIgnoreCase)
                || parent.Name.Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            parent = parent.Parent;
        }

        return false;
    }
}
