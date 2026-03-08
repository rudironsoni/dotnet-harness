using System.CommandLine;

namespace DotnetAgentHarness.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Cross-platform installer for dotnet-agent-harness toolkit");
        
        // TODO: Add install, uninstall, update, self-update commands
        
        rootCommand.AddCommand(new Command("install", "Install the dotnet-agent-harness toolkit"));
        rootCommand.AddCommand(new Command("uninstall", "Remove the dotnet-agent-harness toolkit"));
        rootCommand.AddCommand(new Command("update", "Update the dotnet-agent-harness toolkit"));
        rootCommand.AddCommand(new Command("self-update", "Update this tool to the latest version"));
        
        return await rootCommand.InvokeAsync(args);
    }
}
