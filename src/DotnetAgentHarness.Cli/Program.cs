using System.CommandLine;
using DotnetAgentHarness.Cli.Commands;
using DotnetAgentHarness.Cli.Services;
using DotnetAgentHarness.Cli.Utils;

namespace DotnetAgentHarness.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup dependency injection
        var httpClient = new HttpClient();
        var processRunner = new ProcessRunner();
        
        var prerequisiteChecker = new PrerequisiteChecker(processRunner);
        var rulesyncRunner = new RulesyncRunner(processRunner);
        var configDetector = new ConfigDetector();
        var transactionManager = new TransactionManager();
        var hookDownloader = new HookDownloader(httpClient);

        var rootCommand = new RootCommand("Cross-platform installer for dotnet-agent-harness toolkit")
        {
            new InstallCommand(
                prerequisiteChecker,
                rulesyncRunner,
                configDetector,
                transactionManager,
                hookDownloader),
            
            new UninstallCommand(),
            
            new UpdateCommand(rulesyncRunner, hookDownloader),
            
            new SelfUpdateCommand(),
            
            new Command("version", "Show version information")
            {
                Handler = CommandHandler.Create(() =>
                {
                    Console.WriteLine("dotnet-agent-harness version 1.0.0");
                    return Task.FromResult(0);
                })
            }
        };

        return await rootCommand.InvokeAsync(args);
    }
}
