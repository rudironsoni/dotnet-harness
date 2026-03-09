namespace DotnetAgentHarness.Cli.Commands;

using System.CommandLine;
using DotnetAgentHarness.Cli.Models;

public class UninstallCommand : Command
{
    public UninstallCommand()
        : base("uninstall", "Remove the dotnet-agent-harness toolkit")
    {
        Option<string> pathOption = new(
            new[] { "--path", "-p" },
            () => ".",
            "Directory containing the installation");

        Option<bool> forceOption = new(
            new[] { "--force", "-f" },
            () => false,
            "Skip confirmation prompts");

        Option<bool> cleanOption = new(
            new[] { "--clean", "-c" },
            () => false,
            "Also remove generated files (AGENTS.md, opencode.jsonc, etc.)");

        this.AddOption(pathOption);
        this.AddOption(forceOption);
        this.AddOption(cleanOption);

        this.SetHandler(async (string path, bool force, bool clean) =>
        {
            await ExecuteAsync(path, force, clean);
        }, pathOption, forceOption, cleanOption);
    }

    private static async Task ExecuteAsync(string path, bool force, bool clean)
    {
        string fullPath = Path.GetFullPath(path);
        string rulesyncPath = Path.Combine(fullPath, ".rulesync");

        if (!Directory.Exists(rulesyncPath))
        {
            await Console.Out.WriteLineAsync("No installation found.");
            return;
        }

        await Console.Out.WriteLineAsync($"Uninstalling dotnet-agent-harness toolkit from {fullPath}...");

        if (!force)
        {
            await Console.Out.WriteAsync("  Are you sure? [y/N] ");
            string? response = Console.ReadLine();
            if (!response?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
            {
                await Console.Out.WriteLineAsync("Uninstall cancelled.");
                return;
            }
        }

        try
        {
            // Remove .rulesync directory
            await Console.Out.WriteLineAsync("  Removing .rulesync directory...");
            await Task.Run(() => Directory.Delete(rulesyncPath, true));
            await Console.Out.WriteLineAsync("  ✓ .rulesync removed");

            // Optionally clean generated files
            if (clean)
            {
                await Console.Out.WriteLineAsync("  Removing generated files...");
                string[] filesToClean = new[]
                {
                    "AGENTS.md",
                    "opencode.jsonc",
                    "geminicli.jsonc",
                    "codex.json",
                    Path.Combine(".github", "prompts"),
                    "factory-rules",
                    ".antigravity",
                };

                foreach (string file in filesToClean)
                {
                    string filePath = Path.Combine(fullPath, file);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        await Console.Out.WriteLineAsync($"    ✓ Removed {file}");
                    }
                    else if (Directory.Exists(filePath))
                    {
                        Directory.Delete(filePath, true);
                        await Console.Out.WriteLineAsync($"    ✓ Removed {file}/");
                    }
                }
            }

            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Uninstall complete!");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
