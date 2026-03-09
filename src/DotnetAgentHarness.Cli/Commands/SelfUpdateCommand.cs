namespace DotnetAgentHarness.Cli.Commands;

using System.CommandLine;
using DotnetAgentHarness.Cli.Utils;

public class SelfUpdateCommand : Command
{
    public SelfUpdateCommand()
        : base("self-update", "Update this tool to the latest version")
    {
        Option<bool> forceOption = new(
            new[] { "--force", "-f" },
            () => false,
            "Skip confirmation prompt");

        this.AddOption(forceOption);

        this.SetHandler(async (bool force) =>
        {
            await ExecuteAsync(force);
        }, forceOption);
    }

    private static async Task ExecuteAsync(bool force)
    {
        await Console.Out.WriteLineAsync("Updating dotnet-agent-harness tool...");
        await Console.Out.WriteLineAsync();

        if (!force)
        {
            await Console.Out.WriteAsync("  This will update the global tool. Continue? [y/N] ");
            string? response = Console.ReadLine();
            if (!response?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
            {
                await Console.Out.WriteLineAsync("Update cancelled.");
                return;
            }
        }

        try
        {
            ProcessRunner runner = new();

            await Console.Out.WriteLineAsync("  Running: dotnet tool update -g dotnet-agent-harness");
            ProcessResult result = await runner.RunAsync(
                "dotnet",
                "tool update -g dotnet-agent-harness");

            if (result.ExitCode != 0)
            {
                await Console.Error.WriteLineAsync($"  Update failed: {result.Error}");
                await Console.Error.WriteLineAsync();
                await Console.Error.WriteLineAsync("You can manually update with:");
                await Console.Error.WriteLineAsync("  dotnet tool update -g dotnet-agent-harness");
                Environment.Exit(1);
            }

            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Self-update complete!");
            await Console.Out.WriteLineAsync("Run 'dotnet-agent-harness --version' to verify.");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            await Console.Error.WriteLineAsync();
            await Console.Error.WriteLineAsync("You can manually update with:");
            await Console.Error.WriteLineAsync("  dotnet tool update -g dotnet-agent-harness");
            Environment.Exit(1);
        }
    }
}
