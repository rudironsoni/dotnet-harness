namespace DotnetAgentHarness.Cli;

using System.CommandLine;
using DotnetAgentHarness.Cli.Commands;
using DotnetAgentHarness.Cli.Services;
using DotnetAgentHarness.Cli.Utils;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        // Setup dependency injection (manual for simplicity)
        HttpClient httpClient = new();

        PrerequisiteChecker prerequisiteChecker = new();
        using RulesyncRunner rulesyncRunner = new();
        ConfigDetector configDetector = new();
        TransactionManager transactionManager = new();
        HookDownloader hookDownloader = new(httpClient);

        RootCommand rootCommand = new("Cross-platform installer for dotnet-agent-harness toolkit");

        rootCommand.AddCommand(new InstallCommand(
            prerequisiteChecker,
            rulesyncRunner,
            configDetector,
            transactionManager,
            hookDownloader));

        rootCommand.AddCommand(new UninstallCommand());
        rootCommand.AddCommand(new UpdateCommand(rulesyncRunner, hookDownloader));
        rootCommand.AddCommand(new SelfUpdateCommand());

        Command versionCommand = new("version", "Show version information");
        versionCommand.SetHandler(() =>
        {
            Console.WriteLine("dotnet-agent-harness version 1.0.0");
            return Task.FromResult(0);
        });
        rootCommand.AddCommand(versionCommand);

        return await rootCommand.InvokeAsync(args);
    }
}
