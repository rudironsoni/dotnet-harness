namespace DotnetAgentHarness.Cli.Commands;

using System.CommandLine;
using DotnetAgentHarness.Cli.Models;
using DotnetAgentHarness.Cli.Services;
using DotnetAgentHarness.Cli.Utils;

public class InstallCommand : Command
{
    private readonly IPrerequisiteChecker prerequisiteChecker;
    private readonly IRulesyncRunner rulesyncRunner;
    private readonly IConfigDetector configDetector;
    private readonly ITransactionManager transactionManager;
    private readonly IHookDownloader hookDownloader;

    private static readonly string[] HookScripts = new[]
    {
        "dotnet-agent-harness-session-start.sh",
        "dotnet-agent-harness-post-edit-roslyn.sh",
        "dotnet-agent-harness-slopwatch.sh",
        "dotnet-agent-harness-error-recovery.sh",
        "dotnet-agent-harness-inline-error-recovery.sh",
    };

    public InstallCommand(
        IPrerequisiteChecker prerequisiteChecker,
        IRulesyncRunner rulesyncRunner,
        IConfigDetector configDetector,
        ITransactionManager transactionManager,
        IHookDownloader hookDownloader)
        : base("install", "Install the dotnet-agent-harness toolkit")
    {
        this.prerequisiteChecker = prerequisiteChecker;
        this.rulesyncRunner = rulesyncRunner;
        this.configDetector = configDetector;
        this.transactionManager = transactionManager;
        this.hookDownloader = hookDownloader;

        Option<string> sourceOption = new(
            new[] { "--source", "-s" },
            () => "rudironsoni/dotnet-agent-harness",
            "Source GitHub repository");

        Option<string> targetsOption = new(
            new[] { "--targets", "-t" },
            () => "claudecode,copilot,opencode,geminicli,factorydroid,codexcli,antigravity",
            "Comma-separated list of target platforms");

        Option<string> pathOption = new(
            new[] { "--path", "-p" },
            () => ".",
            "Installation directory");

        Option<bool> forceOption = new(
            new[] { "--force", "-f" },
            () => false,
            "Skip confirmation prompts");

        Option<bool> dryRunOption = new(
            new[] { "--dry-run", "-d" },
            () => false,
            "Show what would be done without making changes");

        Option<bool> verboseOption = new(
            new[] { "--verbose", "-v" },
            () => false,
            "Show detailed output");

        this.AddOption(sourceOption);
        this.AddOption(targetsOption);
        this.AddOption(pathOption);
        this.AddOption(forceOption);
        this.AddOption(dryRunOption);
        this.AddOption(verboseOption);

        this.SetHandler(async (string source, string targets, string path, bool force, bool dryRun, bool verbose) =>
        {
            await this.ExecuteAsync(source, targets, path, force, dryRun, verbose);
        }, sourceOption, targetsOption, pathOption, forceOption, dryRunOption, verboseOption);
    }

    private async Task ExecuteAsync(string source, string targets, string path, bool force, bool dryRun, bool verbose)
    {
        string fullPath = Path.GetFullPath(path);

        await Console.Out.WriteLineAsync("Installing dotnet-agent-harness toolkit...");
        await Console.Out.WriteLineAsync($"  Source: {source}");
        await Console.Out.WriteLineAsync($"  Targets: {targets}");
        await Console.Out.WriteLineAsync($"  Path: {fullPath}");
        if (dryRun)
        {
            await Console.Out.WriteLineAsync("  [DRY RUN - No changes will be made]");
        }

        await Console.Out.WriteLineAsync();

        try
        {
            // Step 1: Check prerequisites
            await Console.Out.WriteLineAsync("==> Checking prerequisites...");
            PrerequisiteResult prereqResult = await this.prerequisiteChecker.CheckAsync();
            if (!prereqResult.Success)
            {
                await Console.Error.WriteLineAsync($"Error: {prereqResult.ErrorMessage}");
                Environment.Exit(1);
            }

            await Console.Out.WriteLineAsync($"  ✓ rulesync {prereqResult.RulesyncVersion} installed");

            // Step 2: Check for existing installation
            string rulesyncPath = Path.Combine(fullPath, ".rulesync");
            string backupPath = string.Empty;

            // S1066: Merge nested if statements
            if (Directory.Exists(rulesyncPath) && !force && !dryRun)
            {
                await Console.Out.WriteAsync("  .rulesync directory already exists. Overwrite? [y/N] ");
                string? response = Console.ReadLine();
                if (!response?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await Console.Out.WriteLineAsync("Installation cancelled.");
                    return;
                }
            }

            // Step 3: Backup (unless dry-run)
            if (!dryRun)
            {
                await Console.Out.WriteLineAsync("==> Creating backup...");
                backupPath = await this.transactionManager.BackupAsync(fullPath);
                if (!string.IsNullOrEmpty(backupPath))
                {
                    await Console.Out.WriteLineAsync($"  ✓ Backup created: {backupPath}");
                }
            }

            // Step 4: Fetch .rulesync
            await Console.Out.WriteLineAsync("==> Fetching .rulesync...");
            if (!dryRun)
            {
                RulesyncResult fetchResult = await this.rulesyncRunner.FetchAsync(source, fullPath);
                if (!fetchResult.Success)
                {
                    await Console.Error.WriteLineAsync($"  ✗ Fetch failed: {fetchResult.Error}");
                    await this.RollbackAsync(backupPath);
                    Environment.Exit(1);
                }
            }

            await Console.Out.WriteLineAsync($"  ✓ Fetched from {source}");

            // Step 5: Check for declarative sources
            await Console.Out.WriteLineAsync("==> Checking for declarative sources...");
            bool hasDeleteTrue = await this.configDetector.HasDeleteTrueAsync(fullPath);
            if (hasDeleteTrue)
            {
                await Console.Out.WriteLineAsync("  ✓ rulesync.jsonc has delete: true");
            }

            // Step 6: Run rulesync install (for declarative sources)
            await Console.Out.WriteLineAsync("==> Installing declarative sources...");
            if (!dryRun)
            {
                RulesyncResult installResult = await this.rulesyncRunner.InstallAsync(fullPath);
                if (!installResult.Success && verbose)
                {
                    await Console.Out.WriteLineAsync($"  Note: {installResult.Error}");
                }
            }

            await Console.Out.WriteLineAsync("  ✓ Install complete");

            // Step 7: Run rulesync generate
            await Console.Out.WriteLineAsync("==> Generating configuration...");
            if (!dryRun)
            {
                RulesyncResult generateResult = await this.rulesyncRunner.GenerateAsync(targets, fullPath, hasDeleteTrue, dryRun);
                if (!generateResult.Success)
                {
                    await Console.Error.WriteLineAsync($"  ✗ Generate failed: {generateResult.Error}");
                    await this.RollbackAsync(backupPath);
                    Environment.Exit(1);
                }
            }

            await Console.Out.WriteLineAsync($"  ✓ Generated for: {targets}");

            // Step 8: Download hooks
            await Console.Out.WriteLineAsync("==> Downloading hook scripts...");
            if (!dryRun)
            {
                HookDownloadResult hooksResult = await this.hookDownloader.DownloadHooksAsync(HookScripts, source, fullPath);
                if (!hooksResult.Success)
                {
                    await Console.Error.WriteLineAsync($"  ✗ Hook download failed: {hooksResult.ErrorMessage}");
                    await this.RollbackAsync(backupPath);
                    Environment.Exit(1);
                }

                // Make hooks executable
                foreach (string hook in hooksResult.DownloadedHooks)
                {
                    string hookPath = Path.Combine(fullPath, ".rulesync", "hooks", hook);
                    PlatformHelper.MakeExecutable(hookPath);
                }
            }

            await Console.Out.WriteLineAsync($"  ✓ Downloaded {HookScripts.Length} hook scripts");

            // Step 9: Cleanup backup on success
            if (!dryRun && !string.IsNullOrEmpty(backupPath))
            {
                await this.transactionManager.CleanupAsync(backupPath);
            }

            // Summary
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Installation Complete!");
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Configuration:");
            await Console.Out.WriteLineAsync($"  Source: {source}");
            await Console.Out.WriteLineAsync($"  Targets: {targets}");
            await Console.Out.WriteLineAsync($"  Path: {fullPath}/.rulesync");
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Next Steps:");
            await Console.Out.WriteLineAsync("  1. Review the generated configuration");
            await Console.Out.WriteLineAsync("  2. Restart your AI coding tool session");
            await Console.Out.WriteLineAsync("  3. Run 'rulesync generate --check' to verify");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"\nError: {ex.Message}");
            if (verbose)
            {
                await Console.Error.WriteLineAsync(ex.StackTrace);
            }

            Environment.Exit(1);
        }
    }

    private async Task RollbackAsync(string backupPath)
    {
        if (string.IsNullOrEmpty(backupPath))
        {
            return;
        }

        await Console.Out.WriteLineAsync("==> Rolling back changes...");
        try
        {
            await this.transactionManager.RestoreAsync(backupPath);
            await Console.Out.WriteLineAsync("  ✓ Rollback complete");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"  ✗ Rollback failed: {ex.Message}");
            await Console.Error.WriteLineAsync($"  Manual restore may be needed from: {backupPath}");
        }
    }
}
