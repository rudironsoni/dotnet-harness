using System.CommandLine;
using DotnetAgentHarness.Cli.Utils;

namespace DotnetAgentHarness.Cli.Commands;

public class SelfUpdateCommand : Command
{
    public SelfUpdateCommand() : base("self-update", "Update this tool to the latest version")
    {
        var forceOption = new Option<bool>(
            new[] { "--force", "-f" },
            () => false,
            "Skip confirmation prompt");

        AddOption(forceOption);

        this.SetHandler(async (bool force) =>
        {
            await ExecuteAsync(force);
        }, forceOption);
    }

    private async Task ExecuteAsync(bool force)
    {
        Console.WriteLine("Updating dotnet-agent-harness tool...");
        Console.WriteLine();

        if (!force)
        {
            Console.Write("  This will update the global tool. Continue? [y/N] ");
            var response = Console.ReadLine();
            if (!response?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
            {
                Console.WriteLine("Update cancelled.");
                return;
            }
        }

        try
        {
            var runner = new ProcessRunner();
            
            Console.WriteLine("  Running: dotnet tool update -g dotnet-agent-harness");
            var result = await runner.RunAsync(
                "dotnet", 
                "tool update -g dotnet-agent-harness");

            if (result.ExitCode != 0)
            {
                Console.Error.WriteLine($"  Update failed: {result.Error}");
                Console.Error.WriteLine();
                Console.Error.WriteLine("You can manually update with:");
                Console.Error.WriteLine("  dotnet tool update -g dotnet-agent-harness");
                Environment.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("Self-update complete!");
            Console.WriteLine("Run 'dotnet-agent-harness --version' to verify.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("You can manually update with:");
            Console.Error.WriteLine("  dotnet tool update -g dotnet-agent-harness");
            Environment.Exit(1);
        }
    }
}
