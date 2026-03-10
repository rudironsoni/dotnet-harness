namespace DotnetAgentHarness.Cli.Commands;

using System.CommandLine;
using System.Text.Json;
using DotnetAgentHarness.Cli.Models;
using DotnetAgentHarness.Cli.Services;

/// <summary>
/// Command to recommend skills based on project analysis.
/// </summary>
public class RecommendCommand : Command
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ISkillCatalog skillCatalog;
    private readonly IProjectAnalyzer projectAnalyzer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendCommand"/> class.
    /// </summary>
    public RecommendCommand(ISkillCatalog skillCatalog, IProjectAnalyzer projectAnalyzer)
        : base("recommend", "Recommend skills for your .NET project")
    {
        this.skillCatalog = skillCatalog;
        this.projectAnalyzer = projectAnalyzer;

        Option<string> pathOption = new(
            ["--path", "-p"],
            () => ".",
            "Path to the project directory or .csproj file");

        Option<string> platformOption = new(
            ["--platform", "-t"],
            "Target platform for recommendations (claudecode, opencode, codexcli, etc.)");

        Option<string> categoryOption = new(
            ["--category", "-c"],
            "Filter recommendations by category");

        Option<int> limitOption = new(
            ["--limit", "-l"],
            () => 10,
            "Maximum recommendations per kind");

        Option<string> formatOption = new(
            ["--format", "-f"],
            () => "text",
            "Output format: text or json");

        Option<bool> writeStateOption = new(
            ["--write-state", "-w"],
            () => false,
            "Write recommendations to .dotnet-agent-harness/recommendations.json");

        this.AddOption(pathOption);
        this.AddOption(platformOption);
        this.AddOption(categoryOption);
        this.AddOption(limitOption);
        this.AddOption(formatOption);
        this.AddOption(writeStateOption);

        this.SetHandler(async (string path, string? platform, string? category, int limit, string format, bool writeState) =>
        {
            await this.ExecuteAsync(path, platform, category, limit, format, writeState);
        }, pathOption, platformOption, categoryOption, limitOption, formatOption, writeStateOption);
    }

    private async Task ExecuteAsync(string path, string? platform, string? category, int limit, string format, bool writeState)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);

            await Console.Out.WriteLineAsync("Analyzing project...");
            await Console.Out.WriteLineAsync();

            // Analyze the project
            ProjectProfile? profile = await this.projectAnalyzer.AnalyzeProjectAsync(fullPath);

            if (profile == null)
            {
                await Console.Error.WriteLineAsync("No .NET project found at the specified path.");
                await Console.Error.WriteLineAsync("Please provide a path to a .csproj file or a directory containing one.");
                Environment.Exit(1);
                return;
            }

            // Display project info
            await DisplayProjectInfoAsync(profile);

            // Generate recommendations
            var recommendations = await this.GenerateRecommendationsAsync(profile, platform, category, limit);

            // Output recommendations
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                await this.OutputJsonAsync(recommendations, profile);
            }
            else
            {
                await OutputTextAsync(recommendations);
            }

            // Write state if requested
            if (writeState)
            {
                await this.WriteStateAsync(recommendations, fullPath);
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task DisplayProjectInfoAsync(ProjectProfile profile)
    {
        await Console.Out.WriteLineAsync("Project Analysis:");
        await Console.Out.WriteLineAsync(new string('-', 50));

        await Console.Out.WriteLineAsync($"  Project: {Path.GetFileName(profile.ProjectPath)}");

        if (!string.IsNullOrEmpty(profile.SolutionPath))
        {
            await Console.Out.WriteLineAsync($"  Solution: {Path.GetFileName(profile.SolutionPath)}");
        }

        await Console.Out.WriteLineAsync($"  Type: {profile.ProjectType}");

        if (profile.TargetFrameworks.Count > 0)
        {
            await Console.Out.WriteLineAsync($"  Frameworks: {string.Join(", ", profile.TargetFrameworks)}");
        }

        if (profile.Packages.Count > 0)
        {
            await Console.Out.WriteLineAsync($"  Packages: {profile.Packages.Count} referenced");
        }

        if (profile.IsTestProject)
        {
            await Console.Out.WriteLineAsync($"  Test Frameworks: {string.Join(", ", profile.TestFrameworks)}");
        }

        if (profile.HasEntityFramework)
        {
            await Console.Out.WriteLineAsync("  Uses: Entity Framework");
        }

        if (profile.HasAspire)
        {
            await Console.Out.WriteLineAsync("  Uses: .NET Aspire");
        }

        if (profile.HasDocker)
        {
            await Console.Out.WriteLineAsync("  Uses: Docker");
        }

        if (profile.CiConfigs.Count > 0)
        {
            var ciPlatforms = profile.CiConfigs.Select(c => c.Platform).Distinct();
            await Console.Out.WriteLineAsync($"  CI/CD: {string.Join(", ", ciPlatforms)}");
        }

        await Console.Out.WriteLineAsync();
    }

    private async Task<RecommendationsResult> GenerateRecommendationsAsync(
        ProjectProfile profile, string? platform, string? category, int limit)
    {
        var result = new RecommendationsResult();

        // Determine search queries based on project characteristics
        var searchTerms = new List<string>();

        if (profile.IsTestProject)
        {
            searchTerms.Add("testing");
            foreach (var tf in profile.TestFrameworks)
            {
                if (tf.Contains("xunit", StringComparison.OrdinalIgnoreCase))
                {
                    searchTerms.Add("xunit");
                }
                else if (tf.Contains("nunit", StringComparison.OrdinalIgnoreCase))
                {
                    searchTerms.Add("nunit");
                }
            }
        }

        if (profile.IsWebProject)
        {
            searchTerms.Add("aspnetcore");
            searchTerms.Add("web");
        }

        if (profile.HasEntityFramework)
        {
            searchTerms.Add("efcore");
            searchTerms.Add("data");
        }

        if (profile.HasAspire)
        {
            searchTerms.Add("aspire");
        }

        if (profile.HasDocker)
        {
            searchTerms.Add("container");
        }

        if (profile.CiConfigs.Any(c => c.Platform.Contains("GitHub")))
        {
            searchTerms.Add("github actions");
        }

        // Add project type specific terms
        switch (profile.ProjectType)
        {
            case "Web":
                searchTerms.Add("web-api");
                break;
            case "Blazor":
                searchTerms.Add("blazor");
                break;
            case "Worker":
                searchTerms.Add("background services");
                break;
        }

        // Search for skills
        var skills = new List<SkillInfo>();
        foreach (string term in searchTerms.Distinct())
        {
            var foundSkills = await this.skillCatalog.SearchSkillsAsync(
                term, category, null, null, platform, limit);
            skills.AddRange(foundSkills);
        }

        // Deduplicate and limit
        result.Skills = skills
            .DistinctBy(s => s.Name)
            .Take(limit)
            .ToList();

        // Search for subagents
        var subagents = new List<SubagentInfo>();
        foreach (string term in searchTerms.Distinct())
        {
            var foundSubagents = await this.skillCatalog.SearchSubagentsAsync(term, platform, limit);
            subagents.AddRange(foundSubagents);
        }

        result.Subagents = subagents
            .DistinctBy(s => s.Name)
            .Take(limit)
            .ToList();

        // Search for commands
        var commands = new List<CommandInfo>();
        foreach (string term in searchTerms.Distinct())
        {
            var foundCommands = await this.skillCatalog.SearchCommandsAsync(term, platform, limit);
            commands.AddRange(foundCommands);
        }

        result.Commands = commands
            .DistinctBy(c => c.Name)
            .Take(limit)
            .ToList();

        return result;
    }

    private static async Task OutputTextAsync(RecommendationsResult recommendations)
    {
        await Console.Out.WriteLineAsync("Recommended Skills:");
        await Console.Out.WriteLineAsync(new string('=', 50));
        await Console.Out.WriteLineAsync();

        if (recommendations.Skills.Count > 0)
        {
            await Console.Out.WriteLineAsync($"Skills ({recommendations.Skills.Count}):");
            await Console.Out.WriteLineAsync(new string('-', 50));

            int i = 1;
            foreach (SkillInfo skill in recommendations.Skills)
            {
                string cat = !string.IsNullOrEmpty(skill.Category) ? $" [{skill.Category}]" : string.Empty;
                await Console.Out.WriteLineAsync($"  {i}. {skill.Name}{cat}");
                if (!string.IsNullOrEmpty(skill.Description))
                {
                    await Console.Out.WriteLineAsync($"     {skill.Description}");
                }

                i++;
            }

            await Console.Out.WriteLineAsync();
        }

        if (recommendations.Subagents.Count > 0)
        {
            await Console.Out.WriteLineAsync($"Subagents ({recommendations.Subagents.Count}):");
            await Console.Out.WriteLineAsync(new string('-', 50));

            int i = 1;
            foreach (SubagentInfo subagent in recommendations.Subagents)
            {
                await Console.Out.WriteLineAsync($"  {i}. {subagent.Name}");
                if (!string.IsNullOrEmpty(subagent.Role))
                {
                    await Console.Out.WriteLineAsync($"     Role: {subagent.Role}");
                }

                if (!string.IsNullOrEmpty(subagent.Description))
                {
                    await Console.Out.WriteLineAsync($"     {subagent.Description}");
                }

                i++;
            }

            await Console.Out.WriteLineAsync();
        }

        if (recommendations.Commands.Count > 0)
        {
            await Console.Out.WriteLineAsync($"Commands ({recommendations.Commands.Count}):");
            await Console.Out.WriteLineAsync(new string('-', 50));

            int i = 1;
            foreach (CommandInfo command in recommendations.Commands)
            {
                await Console.Out.WriteLineAsync($"  {i}. {command.Name}");
                if (!string.IsNullOrEmpty(command.Description))
                {
                    await Console.Out.WriteLineAsync($"     {command.Description}");
                }

                i++;
            }

            await Console.Out.WriteLineAsync();
        }

        int total = recommendations.Skills.Count + recommendations.Subagents.Count + recommendations.Commands.Count;
        if (total == 0)
        {
            await Console.Out.WriteLineAsync("No specific recommendations found for this project.");
            await Console.Out.WriteLineAsync("Try running without filters or use 'dotnet agent-harness search' to browse available skills.");
        }
        else
        {
            await Console.Out.WriteLineAsync($"Total: {total} recommendations");
        }
    }

    private async Task OutputJsonAsync(RecommendationsResult recommendations, ProjectProfile profile)
    {
        var output = new
        {
            Project = new
            {
                profile.ProjectPath,
                profile.ProjectType,
                profile.TargetFrameworks,
                profile.IsTestProject,
                profile.IsWebProject,
                profile.HasEntityFramework,
                profile.HasAspire,
                profile.HasDocker,
            },
            Recommendations = new
            {
                Skills = recommendations.Skills.Select(s => new
                {
                    s.Name,
                    s.Description,
                    s.Category,
                    s.Complexity,
                    s.Tags,
                }),
                Subagents = recommendations.Subagents.Select(s => new
                {
                    s.Name,
                    s.Description,
                    s.Role,
                }),
                Commands = recommendations.Commands.Select(c => new
                {
                    c.Name,
                    c.Description,
                    c.Portability,
                }),
            },
        };

        string json = JsonSerializer.Serialize(output, JsonOptions);
        await Console.Out.WriteLineAsync(json);
    }

    private async Task WriteStateAsync(RecommendationsResult recommendations, string basePath)
    {
        try
        {
            string configDir = Path.Combine(basePath, ".dotnet-agent-harness");
            Directory.CreateDirectory(configDir);

            string statePath = Path.Combine(configDir, "recommendations.json");

            var state = new
            {
                GeneratedAt = DateTime.UtcNow,
                Skills = recommendations.Skills.Select(static s => s.Name).ToList(),
                Subagents = recommendations.Subagents.Select(static s => s.Name).ToList(),
                Commands = recommendations.Commands.Select(static c => c.Name).ToList(),
            };

            await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state, JsonOptions));
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync($"Recommendations saved to: {statePath}");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Warning: Could not write state file: {ex.Message}");
        }
    }

    private sealed class RecommendationsResult
    {
        public List<SkillInfo> Skills { get; set; } = new();

        public List<SubagentInfo> Subagents { get; set; } = new();

        public List<CommandInfo> Commands { get; set; } = new();
    }
}
