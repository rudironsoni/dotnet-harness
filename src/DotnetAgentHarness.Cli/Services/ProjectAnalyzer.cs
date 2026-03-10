namespace DotnetAgentHarness.Cli.Services;

using System.Xml.Linq;
using DotnetAgentHarness.Cli.Models;

/// <summary>
/// Implementation of IProjectAnalyzer that parses .csproj and .sln files using MSBuild APIs.
/// </summary>
public sealed class ProjectAnalyzer : IProjectAnalyzer
{
    /// <inheritdoc />
    public async Task<ProjectProfile?> AnalyzeProjectAsync(string path, CancellationToken ct = default)
    {
        string fullPath = Path.GetFullPath(path);

        // Handle directory - find csproj inside
        if (Directory.Exists(fullPath))
        {
            var projects = await this.FindProjectsAsync(fullPath, ct);
            if (projects.Count == 0)
            {
                return null;
            }

            fullPath = projects[0]; // Analyze first project found
        }

        if (!File.Exists(fullPath) || !fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            string? directory = Path.GetDirectoryName(fullPath);

            // Parse using XDocument (modern replacement for XmlDocument)
            XDocument doc = await XDocument.LoadAsync(
                File.OpenRead(fullPath),
                LoadOptions.None,
                ct);

            XElement? root = doc.Root;

            if (root == null)
            {
                return null;
            }

            var packages = new List<PackageReference>();
            var projects = new List<ProjectReference>();
            var targetFrameworks = new List<string>();
            var ciConfigs = new List<CiConfig>();
            var testFrameworks = new List<string>();

            bool isWebProject = false;
            bool isTestProject = false;
            bool hasEntityFramework = false;
            bool hasAspire = false;

            // Get SDK
            string? sdk = root.Attribute("Sdk")?.Value;

            // Target Framework(s) - handle both single and multiple TFMs
            var tfmElements = root.Descendants()
                .Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                .Select(static e => e.Value)
                .Where(static v => !string.IsNullOrEmpty(v));

            foreach (string? tfm in tfmElements)
            {
                targetFrameworks.AddRange(tfm.Split(';', StringSplitOptions.RemoveEmptyEntries));
            }

            // Package References with proper MSBuild evaluation support
            var packageElements = root.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference");

            foreach (var pkg in packageElements)
            {
                string? name = pkg.Attribute("Include")?.Value;
                string? version = pkg.Attribute("Version")?.Value ?? pkg.Value;
                string? privateAssets = pkg.Attribute("PrivateAssets")?.Value;

                if (!string.IsNullOrEmpty(name))
                {
                    packages.Add(new PackageReference
                    {
                        Name = name,
                        Version = version,
                        IsPrivateAsset = privateAssets?.Equals("all", StringComparison.OrdinalIgnoreCase) == true,
                    });

                    // Detect specific frameworks
                    if (name.Contains("xunit", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("nunit", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("mstest", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("tunit", StringComparison.OrdinalIgnoreCase))
                    {
                        testFrameworks.Add(name);
                    }

                    if (name.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase))
                    {
                        isTestProject = true;
                    }

                    if (name.Contains("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
                    {
                        hasEntityFramework = true;
                    }

                    if (name.Contains("Aspire", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAspire = true;
                    }

                    if (name.Contains("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase))
                    {
                        isWebProject = true;
                    }
                }
            }

            // Project References
            var projectElements = root.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference");

            foreach (var proj in projectElements)
            {
                string? include = proj.Attribute("Include")?.Value;
                if (!string.IsNullOrEmpty(include))
                {
                    string projectName = Path.GetFileNameWithoutExtension(include);
                    projects.Add(new ProjectReference
                    {
                        Path = include,
                        Name = projectName,
                    });
                }
            }

            // Detect project type from SDK
            string? projectType = sdk switch
            {
                var s when s?.Contains("Web", StringComparison.OrdinalIgnoreCase) == true => "Web",
                var s when s?.Contains("Test", StringComparison.OrdinalIgnoreCase) == true => "Test",
                var s when s?.Contains("Worker", StringComparison.OrdinalIgnoreCase) == true => "Worker",
                var s when s?.Contains("Blazor", StringComparison.OrdinalIgnoreCase) == true => "Blazor",
                _ => GetProjectTypeFromFlags(isWebProject, isTestProject),
            };

            static string GetProjectTypeFromFlags(bool isWeb, bool isTest)
            {
                if (isWeb)
                {
                    return "Web";
                }

                if (isTest)
                {
                    return "Test";
                }

                return "ClassLib";
            }

            // Check for OutputType to detect Console apps
            string? outputType = root.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "OutputType")?.Value;

            if (outputType?.Equals("Exe", StringComparison.OrdinalIgnoreCase) == true &&
                projectType == "ClassLib")
            {
                projectType = "Console";
            }

            // Find CI/CD configs
            if (directory != null)
            {
                ciConfigs.AddRange(this.FindCiConfigs(directory));
            }

            // Check for Dockerfile
            bool hasDocker = directory != null && File.Exists(Path.Combine(directory, "Dockerfile"));

            // Find solution
            string? solutionPath = null;
            if (directory != null)
            {
                var solutions = await this.FindSolutionsAsync(directory, ct);
                if (solutions.Count > 0)
                {
                    solutionPath = solutions[0];
                }
            }

            return new ProjectProfile
            {
                ProjectPath = fullPath,
                SolutionPath = solutionPath,
                TargetFrameworks = targetFrameworks.AsReadOnly(),
                ProjectType = projectType,
                Packages = packages.AsReadOnly(),
                Projects = projects.AsReadOnly(),
                CiConfigs = ciConfigs.AsReadOnly(),
                TestFrameworks = testFrameworks.AsReadOnly(),
                IsTestProject = isTestProject || testFrameworks.Count > 0,
                IsWebProject = isWebProject,
                HasEntityFramework = hasEntityFramework,
                HasAspire = hasAspire,
                HasDocker = hasDocker,
            };
        }
        catch (Exception ex)
        {
            // Log error and return null
            await Console.Error.WriteLineAsync($"Error parsing project: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> FindProjectsAsync(string basePath, CancellationToken ct = default)
    {
        var projects = new List<string>();

        if (!Directory.Exists(basePath))
        {
            return Task.FromResult<IReadOnlyList<string>>(projects);
        }

        try
        {
            // Search for .csproj files
            projects.AddRange(Directory.GetFiles(basePath, "*.csproj", SearchOption.AllDirectories));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error finding projects: {ex.Message}");
        }

        return Task.FromResult<IReadOnlyList<string>>(projects);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> FindSolutionsAsync(string basePath, CancellationToken ct = default)
    {
        var solutions = new List<string>();

        if (!Directory.Exists(basePath))
        {
            return Task.FromResult<IReadOnlyList<string>>(solutions);
        }

        try
        {
            // Search for .sln files
            solutions.AddRange(Directory.GetFiles(basePath, "*.sln", SearchOption.TopDirectoryOnly));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error finding solutions: {ex.Message}");
        }

        return Task.FromResult<IReadOnlyList<string>>(solutions);
    }

    private List<CiConfig> FindCiConfigs(string basePath)
    {
        var configs = new List<CiConfig>();

        // GitHub Actions
        string githubPath = Path.Combine(basePath, ".github", "workflows");
        if (Directory.Exists(githubPath))
        {
            foreach (string file in Directory.GetFiles(githubPath, "*.yml")
                .Concat(Directory.GetFiles(githubPath, "*.yaml")))
            {
                configs.Add(new CiConfig { Platform = "GitHub Actions", ConfigPath = file, });
            }
        }

        // Azure DevOps
        string adoPath = Path.Combine(basePath, ".azure-pipelines");
        if (Directory.Exists(adoPath))
        {
            foreach (string file in Directory.GetFiles(adoPath, "*.yml")
                .Concat(Directory.GetFiles(adoPath, "*.yaml")))
            {
                configs.Add(new CiConfig { Platform = "Azure DevOps", ConfigPath = file, });
            }
        }

        // Also check for azure-pipelines.yml in root
        string rootAdoYml = Path.Combine(basePath, "azure-pipelines.yml");
        if (File.Exists(rootAdoYml))
        {
            configs.Add(new CiConfig { Platform = "Azure DevOps", ConfigPath = rootAdoYml, });
        }

        return configs;
    }
}
