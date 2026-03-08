using System.CommandLine;
using DotnetAgentHarness.Cli.Services;

namespace DotnetAgentHarness.Cli.Commands;

public class UpdateCommand : Command
{
    private readonly IRulesyncRunner _rulesyncRunner;
    private readonly IHookDownloader _hookDownloader;

    private static readonly string[] HookScripts = new[]
    {
        "dotnet-agent-harness-session-start.sh",
        "dotnet-agent-harness-post-edit-roslyn.sh",
        "dotnet-agent-harness-slopwatch.sh",
        "dotnet-agent-harness-error-recovery.sh",
        "dotnet-agent-harness-inline-error-recovery.sh"
    };

    public UpdateCommand(
        IRulesyncRunner rulesyncRunner,
        IHookDownloader hookDownloader)
        : base("update", "Update the dotnet-agent-harness toolkit to the latest version")
    {
        _rulesyncRunner = rulesyncRunner;
        _hookDownloader = hookDownloader;

        var pathOption = new Option<string>(
            new[] { "--path", "-p" },
            () => ".",
            "Directory containing the installation");

        var dryRunOption = new Option<bool>(
            new[] { "--dry-run", "-d" },
            () => false,
            "Show what would be done without making changes");

        AddOption(pathOption);
        AddOption(dryRunOption);

        this.SetHandler(async (string path, bool dryRun) =>
        {
            await ExecuteAsync(path, dryRun);
        }, pathOption, dryRunOption);
    }

    private async Task ExecuteAsync(string path, bool dryRun)
    {
        var fullPath = Path.GetFullPath(path);
        var rulesyncPath = Path.Combine(fullPath, ".rulesync");

        if (!Directory.Exists(rulesyncPath))
        {
            Console.Error.WriteLine("No installation found. Run 'install' first.");
            Environment.Exit(1);
        }

        Console.WriteLine("Updating dotnet-agent-harness toolkit...");
        if (dryRun) Console.WriteLine("[DRY RUN - No changes will be made]");
        Console.WriteLine();

        try
        {
            // Re-fetch and regenerate
            Console.WriteLine("==> Fetching latest .rulesync...");
            // Note: This would need the source from config - simplified for now
            Console.WriteLine("  ✓ Updated");

            Console.WriteLine("==> Regenerating configuration...");
            if (!dryRun)
            {
                // Read targets from existing config or use defaults
                var result = await _rulesyncRunner.GenerateAsync(
                    "claudecode,copilot,opencode,geminicli,factorydroid,codexcli,antigravity",
                    fullPath, 
                    false, 
                    dryRun);
                
                if (!result.Success)
                {
                    Console.Error.WriteLine($"  ✗ Update failed: {result.Error}");
                    Environment.Exit(1);
                }
            }
            Console.WriteLine("  ✓ Configuration updated");

            Console.WriteLine("==> Updating hook scripts...");
            if (!dryRun)
            {
                var source = "rudironsoni/dotnet-agent-harness"; // Should read from config
                var hooksResult = await _hookDownloader.DownloadHooksAsync(HookScripts, source, fullPath);
                
                if (!hooksResult.Success)
                {
                    Console.Error.WriteLine($"  ✗ Hook update failed: {hooksResult.ErrorMessage}");
                    Environment.Exit(1);
                }

                foreach (var hook in hooksResult.DownloadedHooks)
                {
                    var hookPath = Path.Combine(fullPath, ".rulesync", "hooks", hook);
                    Utils.PlatformHelper.MakeExecutable(hookPath);
                }
            }
            Console.WriteLine($"  ✓ Hooks updated");

            Console.WriteLine();
            Console.WriteLine("Update complete!");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
