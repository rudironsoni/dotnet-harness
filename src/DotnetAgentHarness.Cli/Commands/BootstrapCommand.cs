namespace DotnetAgentHarness.Cli.Commands;

using System.CommandLine;
using System.Text.Json;
using DotnetAgentHarness.Cli.Services;
using DotnetAgentHarness.Cli.Utils;

/// <summary>
/// Command to bootstrap a new .NET project with agent-harness pre-configured.
/// </summary>
public class BootstrapCommand : Command
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

    /// <summary>
    /// Initializes a new instance of the <see cref="BootstrapCommand"/> class.
    /// </summary>
    public BootstrapCommand(IRulesyncRunner rulesyncRunner, IHookDownloader hookDownloader)
        : base("bootstrap", "Bootstrap a new .NET project with agent-harness pre-configured")
    {
        this.rulesyncRunner = rulesyncRunner;
        this.hookDownloader = hookDownloader;

        Argument<string> nameArgument = new("name", "Name of the new project")
        {
            Arity = ArgumentArity.ExactlyOne,
        };

        Option<string> templateOption = new(
            ["--template", "-t"],
            () => "classlib",
            "Project template (classlib, web-api, web, console, blazor, maui, etc.)");

        Option<string> outputOption = new(
            ["--output", "-o"],
            () => ".",
            "Output directory for the project");

        Option<string> targetsOption = new(
            ["--targets", "-T"],
            () => "claudecode,copilot,opencode",
            "Comma-separated list of target platforms");

        Option<string> sourceOption = new(
            ["--source", "-s"],
            () => "rudironsoni/dotnet-agent-harness",
            "Source GitHub repository");

        Option<bool> skipInstallOption = new(
            ["--skip-install", "-n"],
            () => false,
            "Skip running rulesync install after project creation");

        Option<bool> verboseOption = new(
            ["--verbose", "-v"],
            () => false,
            "Show detailed output");

        this.AddArgument(nameArgument);
        this.AddOption(templateOption);
        this.AddOption(outputOption);
        this.AddOption(targetsOption);
        this.AddOption(sourceOption);
        this.AddOption(skipInstallOption);
        this.AddOption(verboseOption);

        this.SetHandler(async (string name, string template, string output, string targets, string source, bool skipInstall, bool verbose) =>
        {
            await this.ExecuteAsync(name, template, output, targets, source, skipInstall, verbose);
        }, nameArgument, templateOption, outputOption, targetsOption, sourceOption, skipInstallOption, verboseOption);
    }

    private async Task ExecuteAsync(string name, string template, string output, string targets, string source, bool skipInstall, bool verbose)
    {
        try
        {
            string outputPath = Path.GetFullPath(output);
            string projectPath = Path.Combine(outputPath, name);

            await Console.Out.WriteLineAsync($"Bootstrapping new .NET project '{name}'...");
            await Console.Out.WriteLineAsync($"  Template: {template}");
            await Console.Out.WriteLineAsync($"  Output: {projectPath}");
            await Console.Out.WriteLineAsync();

            // Step 1: Create project using dotnet new
            await Console.Out.WriteLineAsync("==> Creating project from template...");
            bool projectCreated = await CreateProjectAsync(name, template, outputPath);
            if (!projectCreated)
            {
                Environment.Exit(1);
                return;
            }

            await Console.Out.WriteLineAsync($"  ✓ Created project: {name}");

            // Step 2: Initialize git repo if not exists
            await Console.Out.WriteLineAsync("==> Initializing git repository...");
            bool gitInitialized = await InitializeGitAsync(projectPath);
            if (gitInitialized)
            {
                await Console.Out.WriteLineAsync("  ✓ Git repository initialized");
            }
            else if (verbose)
            {
                await Console.Out.WriteLineAsync("  (Git repository already exists or git not available)");
            }

            // Step 3: Fetch .rulesync
            await Console.Out.WriteLineAsync("==> Fetching .rulesync...");
            var fetchResult = await this.rulesyncRunner.FetchAsync(source, projectPath);
            if (!fetchResult.Success)
            {
                await Console.Error.WriteLineAsync($"  ✗ Fetch failed: {fetchResult.Error}");
                Environment.Exit(1);
                return;
            }

            await Console.Out.WriteLineAsync($"  ✓ Fetched from {source}");

            // Step 4: Run rulesync install (for declarative sources)
            if (!skipInstall)
            {
                await Console.Out.WriteLineAsync("==> Installing declarative sources...");
                var installResult = await this.rulesyncRunner.InstallAsync(projectPath);
                if (installResult.Success)
                {
                    await Console.Out.WriteLineAsync("  ✓ Declarative sources installed");
                }
                else if (verbose)
                {
                    await Console.Out.WriteLineAsync($"  Note: {installResult.Error}");
                }
            }

            // Step 5: Generate for targets
            await Console.Out.WriteLineAsync("==> Generating configuration...");
            var generateResult = await this.rulesyncRunner.GenerateAsync(targets, projectPath, false, false);
            if (!generateResult.Success)
            {
                await Console.Error.WriteLineAsync($"  ✗ Generate failed: {generateResult.Error}");
                Environment.Exit(1);
                return;
            }

            await Console.Out.WriteLineAsync($"  ✓ Generated for targets: {targets}");

            // Step 6: Download hooks
            await Console.Out.WriteLineAsync("==> Downloading hook scripts...");
            var hooksResult = await this.hookDownloader.DownloadHooksAsync(HookScripts, source, projectPath);
            if (hooksResult.Success)
            {
                foreach (string hook in hooksResult.DownloadedHooks)
                {
                    string hookPath = Path.Combine(projectPath, ".rulesync", "hooks", hook);
                    PlatformHelper.MakeExecutable(hookPath);
                }

                await Console.Out.WriteLineAsync($"  ✓ Downloaded {hooksResult.DownloadedHooks.Length} hook scripts");
            }
            else if (verbose)
            {
                await Console.Out.WriteLineAsync($"  Note: {hooksResult.ErrorMessage}");
            }

            // Step 7: Create starter CLAUDE.md
            await Console.Out.WriteLineAsync("==> Creating starter documentation...");
            await CreateStarterDocsAsync(projectPath, name, template);
            await Console.Out.WriteLineAsync("  ✓ Created CLAUDE.md");

            // Summary
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("╔══════════════════════════════════════════════════════════╗");
            await Console.Out.WriteLineAsync("║              Project Bootstrap Complete!                 ║");
            await Console.Out.WriteLineAsync("╚══════════════════════════════════════════════════════════╝");
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync($"Project created at: {projectPath}");
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Next Steps:");
            await Console.Out.WriteLineAsync($"  cd {name}");
            await Console.Out.WriteLineAsync("  dotnet build");
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Configuration:");
            await Console.Out.WriteLineAsync($"  Targets: {targets}");
            await Console.Out.WriteLineAsync($"  Source: {source}");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            if (verbose)
            {
                await Console.Error.WriteLineAsync(ex.StackTrace);
            }

            Environment.Exit(1);
        }
    }

    private static async Task<bool> CreateProjectAsync(string name, string template, string outputPath)
    {
        try
        {
            var processRunner = new ProcessRunner();
            var result = await processRunner.RunAsync(
                "dotnet",
                $"new {template} -n {name} -o \"{Path.Combine(outputPath, name)}\"",
                outputPath);

            if (result.ExitCode != 0)
            {
                await Console.Error.WriteLineAsync($"  ✗ Failed to create project: {result.Error}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"  ✗ Error creating project: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> InitializeGitAsync(string projectPath)
    {
        try
        {
            // Check if already a git repo
            if (Directory.Exists(Path.Combine(projectPath, ".git")))
            {
                return false;
            }

            var processRunner = new ProcessRunner();
            var result = await processRunner.RunAsync("git", "init", projectPath);

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task CreateStarterDocsAsync(string projectPath, string name, string template)
    {
        string claudeMdPath = Path.Combine(projectPath, "CLAUDE.md");

        string templateDescription = template switch
        {
            "classlib" => "class library",
            "webapi" or "web-api" => "ASP.NET Core Web API",
            "web" or "webapp" => "ASP.NET Core Web Application",
            "console" => "console application",
            "blazor" or "blazorserver" or "blazorwasm" => "Blazor application",
            "maui" => ".NET MAUI application",
            "xunit" => "xUnit test project",
            "nunit" => "NUnit test project",
            "mstest" => "MSTest project",
            _ => $"{template} project",
        };

        string content = $"# {name}\n" +
            "\n" +
            $"This is a .NET {templateDescription} with the dotnet-agent-harness toolkit pre-configured.\n" +
            "\n" +
            "## Getting Started\n" +
            "\n" +
            "```bash\n" +
            "# Build the project\n" +
            "dotnet build\n" +
            "\n" +
            "# Run tests (if applicable)\n" +
            "dotnet test\n" +
            "\n" +
            "# Run the application\n" +
            "dotnet run\n" +
            "```\n" +
            "\n" +
            "## Agent Harness Commands\n" +
            "\n" +
            "This project includes the dotnet-agent-harness CLI. Available commands:\n" +
            "\n" +
            "- `dotnet agent-harness search <query>` - Search skills, subagents, and commands\n" +
            "- `dotnet agent-harness recommend` - Get skill recommendations for your project\n" +
            "- `dotnet agent-harness profile` - Show catalog statistics\n" +
            "\n" +
            "## Project Structure\n" +
            "\n" +
            "```\n" +
            ".\n" +
            "├── .rulesync/          # Agent harness configuration\n" +
            "│   ├── skills/         # Available skills\n" +
            "│   ├── subagents/      # Specialist agents\n" +
            "│   ├── commands/       # Command definitions\n" +
            "│   └── hooks/          # Lifecycle hooks\n" +
            "├── src/                # Source code\n" +
            "└── tests/              # Test projects\n" +
            "```\n" +
            "\n" +
            "## Available Agents\n" +
            "\n" +
            "This project is configured with the following agents:\n" +
            "\n" +
            "- **dotnet-architect** - Architecture and design patterns\n" +
            "- **dotnet-testing-specialist** - Testing guidance and patterns\n" +
            "- **dotnet-aspnetcore-specialist** - ASP.NET Core expertise (if web project)\n" +
            "- **dotnet-performance-analyst** - Performance optimization\n" +
            "- **dotnet-security-reviewer** - Security best practices\n" +
            "\n" +
            "## Next Steps\n" +
            "\n" +
            "1. Review the generated `.rulesync/` configuration\n" +
            "2. Run `dotnet agent-harness recommend` to get tailored skill suggestions\n" +
            "3. Start coding with AI assistance!\n" +
            "\n" +
            "## Documentation\n" +
            "\n" +
            "- [RuleSync Documentation](https://github.com/rudironsoni/dotnet-agent-harness/tree/main/.rulesync/skills/rulesync)\n" +
            "- [.NET Skills Index](.rulesync/skills/INDEX.md)\n" +
            "\n" +
            "---\n" +
            "*Generated by dotnet-agent-harness bootstrap*\n";

        await File.WriteAllTextAsync(claudeMdPath, content);
    }
}
