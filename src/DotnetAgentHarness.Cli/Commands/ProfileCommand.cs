namespace DotnetAgentHarness.Cli.Commands;

using System.CommandLine;
using System.Text.Json;
using DotnetAgentHarness.Cli.Models;
using DotnetAgentHarness.Cli.Services;
using Spectre.Console;

/// <summary>
/// Command to show catalog statistics and item details.
/// </summary>
public class ProfileCommand : Command
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ISkillCatalog skillCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileCommand"/> class.
    /// </summary>
    public ProfileCommand(ISkillCatalog skillCatalog)
        : base("profile", "Show catalog statistics or detailed info about a specific item")
    {
        this.skillCatalog = skillCatalog;

        Argument<string?> itemArgument = new("item", "Optional item name to get detailed info for")
        {
            Arity = ArgumentArity.ZeroOrOne,
        };

        Option<string> kindOption = new(
            ["--kind", "-k"],
            "Item kind: skill, subagent, or command (required with item name)");

        Option<string> formatOption = new(
            ["--format", "-f"],
            () => "text",
            "Output format: text or json");

        this.AddArgument(itemArgument);
        this.AddOption(kindOption);
        this.AddOption(formatOption);

        this.SetHandler(async (string? item, string? kind, string format) =>
        {
            await this.ExecuteAsync(item, kind, format);
        }, itemArgument, kindOption, formatOption);
    }

    private async Task ExecuteAsync(string? item, string? kind, string format)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                // Show overall catalog statistics
                CatalogStats stats = await this.skillCatalog.GetStatsAsync();

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    await OutputStatsJsonAsync(stats);
                }
                else
                {
                    OutputStatsText(stats);
                }
            }
            else
            {
                // Show specific item details
                object? itemDetails = await this.GetItemAsync(item, kind);

                if (itemDetails == null)
                {
                    await Console.Error.WriteLineAsync($"Item not found: {item}");
                    Environment.Exit(1);
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    await OutputItemJsonAsync(itemDetails);
                }
                else
                {
                    OutputItemText(itemDetails);
                }
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private async Task<object?> GetItemAsync(string item, string? kind)
    {
        // If kind is specified, search only that kind
        if (!string.IsNullOrWhiteSpace(kind))
        {
            return kind.ToLowerInvariant() switch
            {
                "skill" => await this.skillCatalog.GetSkillByNameAsync(item),
                "subagent" => await this.skillCatalog.GetSubagentByNameAsync(item),
                "command" => await this.skillCatalog.GetCommandByNameAsync(item),
                _ => null,
            };
        }

        // Otherwise search all kinds and return first match
        var skill = await this.skillCatalog.GetSkillByNameAsync(item);
        if (skill != null)
        {
            return skill;
        }

        var subagent = await this.skillCatalog.GetSubagentByNameAsync(item);
        if (subagent != null)
        {
            return subagent;
        }

        var command = await this.skillCatalog.GetCommandByNameAsync(item);
        return command;
    }

    private static void OutputStatsText(CatalogStats stats)
    {
        // Header panel
        AnsiConsole.Write(
            new Panel("[bold blue]Dotnet Agent Harness Catalog Profile[/]")
                .Header("[bold white]Catalog Statistics[/]")
                .BorderColor(Color.Blue)
                .Expand());

        AnsiConsole.WriteLine();

        // Summary table
        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric", c => c.Width(20))
            .AddColumn("Count", c => c.Width(10).RightAligned());

        summaryTable.AddRow("[green]Total Skills[/]", stats.TotalSkills.ToString());
        summaryTable.AddRow("[green]Total Subagents[/]", stats.TotalSubagents.ToString());
        summaryTable.AddRow("[green]Total Commands[/]", stats.TotalCommands.ToString());
        summaryTable.AddRow("[green]Total Lines[/]", stats.TotalLines.ToString("N0"));
        summaryTable.AddRow("[green]Unique Tags[/]", stats.TotalTags.ToString());

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();

        // Skills by category with bar chart
        if (stats.SkillsByCategory.Count > 0)
        {
            var categoryTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Category", c => c.Width(25))
                .AddColumn("Count", c => c.Width(8).RightAligned())
                .AddColumn("Distribution", c => c.Width(40));

            foreach (var category in stats.SkillsByCategory.OrderByDescending(kvp => kvp.Value).Take(10))
            {
                int barLength = Math.Min(category.Value, 35);
                string bar = new('█', barLength);
                string color = category.Value switch
                {
                    > 30 => "red",
                    > 15 => "yellow",
                    _ => "green",
                };
                categoryTable.AddRow(
                    category.Key,
                    category.Value.ToString(),
                    $"[{color}]{bar}[/]");
            }

            AnsiConsole.Write(categoryTable);
            AnsiConsole.WriteLine();
        }

        // Skills by complexity
        if (stats.SkillsByComplexity.Count > 0)
        {
            var complexityTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Complexity", c => c.Width(25))
                .AddColumn("Count", c => c.Width(8).RightAligned());

            foreach (var complexity in stats.SkillsByComplexity.OrderBy(kvp =>
                kvp.Key switch { "beginner" => 1, "intermediate" => 2, "advanced" => 3, _ => 4 }))
            {
                string color = complexity.Key switch
                {
                    "beginner" => "green",
                    "intermediate" => "yellow",
                    "advanced" => "red",
                    _ => "grey",
                };
                complexityTable.AddRow(
                    $"[{color}]{complexity.Key}[/]",
                    complexity.Value.ToString());
            }

            AnsiConsole.Write(complexityTable);
            AnsiConsole.WriteLine();
        }

        // Top tags
        if (stats.TopTags.Count > 0)
        {
            var tagsTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Tag", c => c.Width(35))
                .AddColumn("Count", c => c.Width(10).RightAligned());

            foreach (var tag in stats.TopTags.Take(10))
            {
                tagsTable.AddRow(tag.Key, tag.Value.ToString());
            }

            AnsiConsole.Write(
                new Panel(tagsTable)
                    .Header("[bold white]Top Tags[/]")
                    .BorderColor(Color.Green));
            AnsiConsole.WriteLine();
        }

        // Token estimate
        double estimatedTokens = stats.TotalLines * 4;
        AnsiConsole.Write(new Panel($"Estimated Tokens: [bold]{estimatedTokens:N0}[/] ( ~4 tokens/line )")
            .BorderColor(Color.Grey));
    }

    private static async Task OutputStatsJsonAsync(CatalogStats stats)
    {
        string json = JsonSerializer.Serialize(stats, JsonOptions);
        await Console.Out.WriteLineAsync(json);
    }

    private static void OutputItemText(object item)
    {
        AnsiConsole.Write(
            new Panel("[bold blue]Catalog Item Details[/]")
                .Header("[bold white]Item Information[/]")
                .BorderColor(Color.Blue)
                .Expand());

        AnsiConsole.WriteLine();

        switch (item)
        {
            case SkillInfo skill:
                OutputSkillDetails(skill);
                break;
            case SubagentInfo subagent:
                OutputSubagentDetails(subagent);
                break;
            case CommandInfo command:
                OutputCommandDetails(command);
                break;
        }
    }

    private static void OutputSkillDetails(SkillInfo skill)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property", c => c.Width(15))
            .AddColumn("Value");

        table.AddRow("Type", "[green]Skill[/]");
        table.AddRow("Name", skill.Name);
        table.AddRow("Category", skill.Category ?? "[grey]N/A[/]");
        table.AddRow("Subcategory", skill.Subcategory ?? "[grey]N/A[/]");
        table.AddRow("Complexity", skill.Complexity ?? "[grey]N/A[/]");
        table.AddRow("Version", skill.Version ?? "[grey]N/A[/]");
        table.AddRow("Author", skill.Author ?? "[grey]N/A[/]");
        table.AddRow("Invocable", skill.Invocable ? "[green]Yes[/]" : "[red]No[/]");
        table.AddRow("Lines", skill.LineCount.ToString());

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (!string.IsNullOrEmpty(skill.Description))
        {
            AnsiConsole.Write(
                new Panel(skill.Description)
                    .Header("[bold white]Description[/]")
                    .BorderColor(Color.Green));
            AnsiConsole.WriteLine();
        }

        if (skill.Tags.Count > 0)
        {
            AnsiConsole.WriteLine("Tags: " + string.Join(", ", skill.Tags.Select(t => $"[blue]{t}[/]")));
        }

        if (skill.Targets.Count > 0)
        {
            AnsiConsole.WriteLine("Targets: " + string.Join(", ", skill.Targets.Select(t => $"[yellow]{t}[/]")));
        }

        if (skill.RelatedSkills.Count > 0)
        {
            AnsiConsole.WriteLine("Related Skills:");
            foreach (string related in skill.RelatedSkills)
            {
                AnsiConsole.WriteLine($"  • {related}");
            }
        }

        if (!string.IsNullOrEmpty(skill.FilePath))
        {
            AnsiConsole.WriteLine($"[grey]File: {skill.FilePath}[/]");
        }
    }

    private static void OutputSubagentDetails(SubagentInfo subagent)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property", c => c.Width(15))
            .AddColumn("Value");

        table.AddRow("Type", "[purple]Subagent[/]");
        table.AddRow("Name", subagent.Name);
        table.AddRow("Role", subagent.Role ?? "[grey]N/A[/]");
        table.AddRow("Version", subagent.Version ?? "[grey]N/A[/]");
        table.AddRow("Author", subagent.Author ?? "[grey]N/A[/]");
        table.AddRow("Lines", subagent.LineCount.ToString());

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (!string.IsNullOrEmpty(subagent.Description))
        {
            AnsiConsole.Write(
                new Panel(subagent.Description)
                    .Header("[bold white]Description[/]")
                    .BorderColor(Color.Green));
            AnsiConsole.WriteLine();
        }

        if (subagent.Tags.Count > 0)
        {
            AnsiConsole.WriteLine("Tags: " + string.Join(", ", subagent.Tags.Select(t => $"[blue]{t}[/]")));
        }

        if (subagent.Targets.Count > 0)
        {
            AnsiConsole.WriteLine("Targets: " + string.Join(", ", subagent.Targets.Select(t => $"[yellow]{t}[/]")));
        }

        if (!string.IsNullOrEmpty(subagent.FilePath))
        {
            AnsiConsole.WriteLine($"[grey]File: {subagent.FilePath}[/]");
        }
    }

    private static void OutputCommandDetails(CommandInfo command)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property", c => c.Width(17))
            .AddColumn("Value");

        table.AddRow("Type", "[yellow]Command[/]");
        table.AddRow("Name", command.Name);
        table.AddRow("Portability", command.Portability ?? "[grey]N/A[/]");
        table.AddRow("Flattening Risk", command.FlatteningRisk ?? "[grey]N/A[/]");
        table.AddRow("Simulated", command.Simulated ? "[green]Yes[/]" : "[red]No[/]");
        table.AddRow("Version", command.Version ?? "[grey]N/A[/]");
        table.AddRow("Author", command.Author ?? "[grey]N/A[/]");
        table.AddRow("Lines", command.LineCount.ToString());

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (!string.IsNullOrEmpty(command.Description))
        {
            AnsiConsole.Write(
                new Panel(command.Description)
                    .Header("[bold white]Description[/]")
                    .BorderColor(Color.Green));
            AnsiConsole.WriteLine();
        }

        if (command.Tags.Count > 0)
        {
            AnsiConsole.WriteLine("Tags: " + string.Join(", ", command.Tags.Select(t => $"[blue]{t}[/]")));
        }

        if (command.Targets.Count > 0)
        {
            AnsiConsole.WriteLine("Targets: " + string.Join(", ", command.Targets.Select(t => $"[yellow]{t}[/]")));
        }

        if (!string.IsNullOrEmpty(command.FilePath))
        {
            AnsiConsole.WriteLine($"[grey]File: {command.FilePath}[/]");
        }
    }

    private static async Task OutputItemJsonAsync(object item)
    {
        string json = JsonSerializer.Serialize(item, JsonOptions);
        await Console.Out.WriteLineAsync(json);
    }
}
