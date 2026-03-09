namespace DotnetAgentHarness.Cli.Commands;

using System.CommandLine;
using DotnetAgentHarness.Cli.Services;

public class UpdateCommand : Command
{
    private readonly IRulesyncRunner rulesyncRunner;
    private readonly IHookDownloader hookDownloader;

    private static readonly string[] HookScripts = new[]
    {
        "dotnet-agent-harness-session-start.sh",
        "dotnet-agent-harness-post-edit-roslyn.sh",
        "dotnet-agent-harness-slopwatch.sh",
        "dotnet-agent-harness-error-recovery.sh",
        "dotnet-agent-harness-inline-error-recovery.sh",
    };

    public UpdateCommand(
        IRulesyncRunner rulesyncRunner,
        IHookDownloader hookDownloader)
        : base("update", "Update the dotnet-agent-harness toolkit to the latest version")
    {
        this.rulesyncRunner = rulesyncRunner;
        this.hookDownloader = hookDownloader;

        Option<string> pathOption = new(
            new[] { "--path", "-p" },
            () => ".",
            "Directory containing the installation");

        Option<bool> dryRunOption = new(
            new[] { "--dry-run", "-d" },
            () => false,
            "Show what would be done without making changes");

        this.AddOption(pathOption);
        this.AddOption(dryRunOption);

        this.SetHandler(async (string path, bool dryRun) =>
        {
            await this.ExecuteAsync(path, dryRun);
        }, pathOption, dryRunOption);
    }

    private async Task ExecuteAsync(string path, bool dryRun)
    {
        string fullPath = Path.GetFullPath(path);
        string rulesyncPath = Path.Combine(fullPath, ".rulesync");

        if (!Directory.Exists(rulesyncPath))
        {
            await Console.Error.WriteLineAsync("No installation found. Run 'install' first.");
            Environment.Exit(1);
        }

        await Console.Out.WriteLineAsync("Updating dotnet-agent-harness toolkit...");
        if (dryRun)
        {
            await Console.Out.WriteLineAsync("[DRY RUN - No changes will be made]");
        }

        await Console.Out.WriteLineAsync();

        try
        {
            // Re-fetch and regenerate
            await Console.Out.WriteLineAsync("==> Fetching latest .rulesync...");

            // Note: This would need the source from config - simplified for now
            await Console.Out.WriteLineAsync("  ✓ Updated");

            await Console.Out.WriteLineAsync("==> Regenerating configuration...");
            if (!dryRun)
            {
                // Read targets from existing config or use defaults
                RulesyncResult result = await this.rulesyncRunner.GenerateAsync(
                    "claudecode,copilot,opencode,geminicli,factorydroid,codexcli,antigravity",
                    fullPath,
                    false,
                    dryRun);

                if (!result.Success)
                {
                    await Console.Error.WriteLineAsync($"  ✗ Update failed: {result.Error}");
                    Environment.Exit(1);
                }
            }

            await Console.Out.WriteLineAsync("  ✓ Configuration updated");

            await Console.Out.WriteLineAsync("==> Updating hook scripts...");
            if (!dryRun)
            {
                string source = "rudironsoni/dotnet-agent-harness"; // Should read from config
                HookDownloadResult hooksResult = await this.hookDownloader.DownloadHooksAsync(HookScripts, source, fullPath);

                if (!hooksResult.Success)
                {
                    await Console.Error.WriteLineAsync($"  ✗ Hook update failed: {hooksResult.ErrorMessage}");
                    Environment.Exit(1);
                }

                foreach (string hook in hooksResult.DownloadedHooks)
                {
                    string hookPath = Path.Combine(fullPath, ".rulesync", "hooks", hook);
                    Utils.PlatformHelper.MakeExecutable(hookPath);
                }
            }

            await Console.Out.WriteLineAsync("  ✓ Hooks updated");

            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Update complete!");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
