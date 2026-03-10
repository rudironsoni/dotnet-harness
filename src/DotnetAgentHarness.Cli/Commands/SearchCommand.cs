namespace DotnetAgentHarness.Cli.Commands;

using System.CommandLine;
using System.Text.Json;
using DotnetAgentHarness.Cli.Models;
using DotnetAgentHarness.Cli.Services;

/// <summary>
/// Command to search skills, subagents, and commands by keyword.
/// </summary>
public class SearchCommand : Command
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ISkillCatalog skillCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchCommand"/> class.
    /// </summary>
    public SearchCommand(ISkillCatalog skillCatalog)
        : base("search", "Search skills, subagents, and commands by keyword")
    {
        this.skillCatalog = skillCatalog;

        Argument<string> queryArgument = new("query", "Search query (keyword or phrase)")
        {
            Arity = ArgumentArity.ZeroOrOne,
        };

        Option<string> kindOption = new(
            ["--kind", "-k"],
            () => "all",
            "Filter by kind: skill, subagent, command, or all");

        Option<string> categoryOption = new(
            ["--category", "-c"],
            "Filter by category (for skills)");

        Option<string> platformOption = new(
            ["--platform", "-p"],
            "Filter by platform compatibility");

        Option<int> limitOption = new(
            ["--limit", "-l"],
            () => 10,
            "Maximum number of results to return");

        Option<string> formatOption = new(
            ["--format", "-f"],
            () => "text",
            "Output format: text or json");

        this.AddArgument(queryArgument);
        this.AddOption(kindOption);
        this.AddOption(categoryOption);
        this.AddOption(platformOption);
        this.AddOption(limitOption);
        this.AddOption(formatOption);

        this.SetHandler(async (string? query, string kind, string? category, string? platform, int limit, string format) =>
        {
            await this.ExecuteAsync(query, kind, category, platform, limit, format);
        }, queryArgument, kindOption, categoryOption, platformOption, limitOption, formatOption);
    }

    private async Task ExecuteAsync(string? query, string kind, string? category, string? platform, int limit, string format)
    {
        try
        {
            var results = new SearchResults();

            // Search skills
            if (kind is "all" or "skill")
            {
                results.Skills = (await this.skillCatalog.SearchSkillsAsync(
                    query, category, null, null, platform, limit)).ToList();
            }

            // Search subagents
            if (kind is "all" or "subagent")
            {
                results.Subagents = (await this.skillCatalog.SearchSubagentsAsync(
                    query, platform, limit)).ToList();
            }

            // Search commands
            if (kind is "all" or "command")
            {
                results.Commands = (await this.skillCatalog.SearchCommandsAsync(
                    query, platform, limit)).ToList();
            }

            // Output results
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                await OutputJsonAsync(results);
            }
            else
            {
                await OutputTextAsync(results, query);
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task OutputTextAsync(SearchResults results, string? query)
    {
        string searchTerm = string.IsNullOrWhiteSpace(query) ? "(all)" : $"\"{query}\"";
        await Console.Out.WriteLineAsync($"Search results for {searchTerm}:");
        await Console.Out.WriteLineAsync();

        int totalCount = results.Skills.Count + results.Subagents.Count + results.Commands.Count;

        if (totalCount == 0)
        {
            await Console.Out.WriteLineAsync("No results found.");
            return;
        }

        // Output skills
        if (results.Skills.Count > 0)
        {
            await Console.Out.WriteLineAsync($"Skills ({results.Skills.Count}):");
            await Console.Out.WriteLineAsync(new string('-', 60));

            int i = 1;
            foreach (SkillInfo skill in results.Skills)
            {
                string category = !string.IsNullOrEmpty(skill.Category) ? $" [{skill.Category}]" : string.Empty;
                await Console.Out.WriteLineAsync($"  {i}. {skill.Name}{category}");

                if (!string.IsNullOrEmpty(skill.Description))
                {
                    await Console.Out.WriteLineAsync($"     {skill.Description}");
                }

                if (skill.Tags.Count > 0)
                {
                    await Console.Out.WriteLineAsync($"     Tags: {string.Join(", ", skill.Tags)}");
                }

                i++;
            }

            await Console.Out.WriteLineAsync();
        }

        // Output subagents
        if (results.Subagents.Count > 0)
        {
            await Console.Out.WriteLineAsync($"Subagents ({results.Subagents.Count}):");
            await Console.Out.WriteLineAsync(new string('-', 60));

            int i = 1;
            foreach (SubagentInfo subagent in results.Subagents)
            {
                await Console.Out.WriteLineAsync($"  {i}. {subagent.Name}");

                if (!string.IsNullOrEmpty(subagent.Description))
                {
                    await Console.Out.WriteLineAsync($"     {subagent.Description}");
                }

                if (!string.IsNullOrEmpty(subagent.Role))
                {
                    await Console.Out.WriteLineAsync($"     Role: {subagent.Role}");
                }

                i++;
            }

            await Console.Out.WriteLineAsync();
        }

        // Output commands
        if (results.Commands.Count > 0)
        {
            await Console.Out.WriteLineAsync($"Commands ({results.Commands.Count}):");
            await Console.Out.WriteLineAsync(new string('-', 60));

            int i = 1;
            foreach (CommandInfo command in results.Commands)
            {
                string portability = !string.IsNullOrEmpty(command.Portability) ? $" [{command.Portability}]" : string.Empty;
                await Console.Out.WriteLineAsync($"  {i}. {command.Name}{portability}");

                if (!string.IsNullOrEmpty(command.Description))
                {
                    await Console.Out.WriteLineAsync($"     {command.Description}");
                }

                if (command.Simulated)
                {
                    await Console.Out.WriteLineAsync("     (simulated)");
                }

                i++;
            }

            await Console.Out.WriteLineAsync();
        }

        await Console.Out.WriteLineAsync($"Total: {totalCount} results");
    }

    private static async Task OutputJsonAsync(SearchResults results)
    {
        string json = JsonSerializer.Serialize(results, JsonOptions);
        await Console.Out.WriteLineAsync(json);
    }

    private sealed class SearchResults
    {
        public List<SkillInfo> Skills { get; set; } = new();

        public List<SubagentInfo> Subagents { get; set; } = new();

        public List<CommandInfo> Commands { get; set; } = new();
    }
}
