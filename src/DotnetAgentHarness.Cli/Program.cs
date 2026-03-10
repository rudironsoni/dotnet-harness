namespace DotnetAgentHarness.Cli;

using System.CommandLine;
using DotnetAgentHarness.Cli.Commands;
using DotnetAgentHarness.Cli.Services;
using System.IO.Abstractions;
using DotnetAgentHarness.Cli.Utils;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IPrerequisiteChecker, PrerequisiteChecker>();
        services.AddSingleton<IRulesyncRunner, RulesyncRunner>();
        services.AddSingleton<IConfigDetector, ConfigDetector>();
        services.AddSingleton<ITransactionManager, TransactionManager>();
        services.AddSingleton<IHookDownloader, HookDownloader>();
        services.AddSingleton<ISkillCatalog, SkillCatalog>();
        services.AddSingleton<IProjectAnalyzer, ProjectAnalyzer>();

        // Register commands
        services.AddTransient<InstallCommand>();
        services.AddTransient<UninstallCommand>();
        services.AddTransient<UpdateCommand>();
        services.AddTransient<SelfUpdateCommand>();
        services.AddTransient<SearchCommand>();
        services.AddTransient<ProfileCommand>();
        services.AddTransient<RecommendCommand>();
        services.AddTransient<BootstrapCommand>();

        var provider = services.BuildServiceProvider();

        var rootCommand = new RootCommand("Cross-platform installer for dotnet-agent-harness toolkit");

        // Lifecycle commands
        rootCommand.AddCommand(provider.GetRequiredService<InstallCommand>());
        rootCommand.AddCommand(provider.GetRequiredService<UninstallCommand>());
        rootCommand.AddCommand(provider.GetRequiredService<UpdateCommand>());
        rootCommand.AddCommand(provider.GetRequiredService<SelfUpdateCommand>());

        // Discovery commands
        rootCommand.AddCommand(provider.GetRequiredService<SearchCommand>());
        rootCommand.AddCommand(provider.GetRequiredService<ProfileCommand>());
        rootCommand.AddCommand(provider.GetRequiredService<RecommendCommand>());

        // Project commands
        rootCommand.AddCommand(provider.GetRequiredService<BootstrapCommand>());

        var versionCommand = new Command("version", "Show version information");
        versionCommand.SetHandler(() =>
        {
            Console.WriteLine("dotnet-agent-harness version 1.0.0");
            return Task.FromResult(0);
        });
        rootCommand.AddCommand(versionCommand);

        return await rootCommand.InvokeAsync(args);
    }
}
