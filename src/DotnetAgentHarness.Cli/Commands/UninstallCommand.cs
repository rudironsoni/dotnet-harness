using System.CommandLine;
using DotnetAgentHarness.Cli.Models;

namespace DotnetAgentHarness.Cli.Commands;

public class UninstallCommand : Command
{
    public UninstallCommand() : base("uninstall", "Remove the dotnet-agent-harness toolkit")
    {
        var pathOption = new Option<string>(
            new[] { "--path", "-p" },
            () => ".",
            "Directory containing the installation");

        var forceOption = new Option<bool>(
            new[] { "--force", "-f" },
            () => false,
            "Skip confirmation prompts");

        var cleanOption = new Option<bool>(
            new[] { "--clean", "-c" },
            () => false,
            "Also remove generated files (AGENTS.md, opencode.jsonc, etc.)");

        AddOption(pathOption);
        AddOption(forceOption);
        AddOption(cleanOption);

        this.SetHandler(async (string path, bool force, bool clean) =>
        {
            await ExecuteAsync(path, force, clean);
        }, pathOption, forceOption, cleanOption);
    }

    private async Task ExecuteAsync(string path, bool force, bool clean)
    {
        var fullPath = Path.GetFullPath(path);
        var rulesyncPath = Path.Combine(fullPath, ".rulesync");

        if (!Directory.Exists(rulesyncPath))
        {
            Console.WriteLine("No installation found.");
            return;
        }

        Console.WriteLine($"Uninstalling dotnet-agent-harness toolkit from {fullPath}...");

        if (!force)
        {
            Console.Write("  Are you sure? [y/N] ");
            var response = Console.ReadLine();
            if (!response?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
            {
                Console.WriteLine("Uninstall cancelled.");
                return;
            }
        }

        try
        {
            // Remove .rulesync directory
            Console.WriteLine("  Removing .rulesync directory...");
            await Task.Run(() => Directory.Delete(rulesyncPath, true));
            Console.WriteLine("  ✓ .rulesync removed");

            // Optionally clean generated files
            if (clean)
            {
                Console.WriteLine("  Removing generated files...");
                var filesToClean = new[]
                {
                    "AGENTS.md",
                    "opencode.jsonc",
                    "geminicli.jsonc",
                    "codex.json",
                    Path.Combine(".github", "prompts"),
                    "factory-rules",
                    ".antigravity"
                };

                foreach (var file in filesToClean)
                {
                    var filePath = Path.Combine(fullPath, file);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        Console.WriteLine($"    ✓ Removed {file}");
                    }
                    else if (Directory.Exists(filePath))
                    {
                        Directory.Delete(filePath, true);
                        Console.WriteLine($"    ✓ Removed {file}/");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Uninstall complete!");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
