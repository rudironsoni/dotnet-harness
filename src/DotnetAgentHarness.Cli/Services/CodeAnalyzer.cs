namespace DotnetAgentHarness.Cli.Services;

using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using DotnetAgentHarness.Cli.Models;

/// <summary>
/// Implementation of ICodeAnalyzer that runs Roslyn analyzers, StyleCop, and Sonar analysis.
/// </summary>
public sealed class CodeAnalyzer : ICodeAnalyzer
{
    private readonly IProjectAnalyzer projectAnalyzer;
    private readonly IFileSystem fileSystem;

    // Regex patterns for parsing MSBuild output
    // Matches: FilePath(Line,Column): error|warning|info CODE: Message
    private static readonly Regex MsBuildPattern = new(
        @"^(.*?)(?:\((\d+),\s*(\d+)\))?\s*:\s*(error|warning|info)\s+([A-Z]{2,}\d{3,})\s*:\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // StyleCop specific pattern (often includes rule name)
    private static readonly Regex StyleCopPattern = new(
        @"\[SA(\d{4})\]",
        RegexOptions.Compiled);

    // Sonar specific pattern
    private static readonly Regex SonarPattern = new(
        @"\[S(\d{4,6})\]",
        RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeAnalyzer"/> class.
    /// </summary>
    public CodeAnalyzer(IProjectAnalyzer projectAnalyzer, IFileSystem fileSystem)
    {
        this.projectAnalyzer = projectAnalyzer;
        this.fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async Task<AnalysisResult> AnalyzeAsync(AnalysisOptions options, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new AnalysisResult { Success = true };

        try
        {
            string projectPath = ResolveProjectPath(options.ProjectPath);

            if (options.Verbose)
            {
                await Console.Out.WriteLineAsync($"Analyzing: {projectPath}");
            }

            // Find all projects to analyze
            var projects = await this.FindProjectsToAnalyzeAsync(projectPath, ct);

            if (projects.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No .csproj files found to analyze.";
                return result;
            }

            if (options.Verbose)
            {
                await Console.Out.WriteLineAsync($"Found {projects.Count} project(s) to analyze");
            }

            // Run analysis on each project
            foreach (string project in projects)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                await this.AnalyzeProjectAsync(project, options, result, ct);
            }

            // Calculate statistics
            result.TotalIssues = result.Issues.Count;
            result.ErrorCount = result.Issues.Count(i => i.Severity == AnalysisSeverity.Error);
            result.WarningCount = result.Issues.Count(i => i.Severity == AnalysisSeverity.Warning);
            result.InfoCount = result.Issues.Count(i => i.Severity == AnalysisSeverity.Info);

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> HasAnalyzersConfiguredAsync(string projectPath)
    {
        if (!this.fileSystem.File.Exists(projectPath))
        {
            return false;
        }

        try
        {
            string content = await this.fileSystem.File.ReadAllTextAsync(projectPath);

            // Check for common analyzer packages
            return content.Contains("Microsoft.CodeAnalysis.Analyzers", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("StyleCop.Analyzers", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("SonarAnalyzer.CSharp", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("Roslynator.Analyzers", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("<EnableNETAnalyzers", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<IReadOnlyList<string>> FindProjectsToAnalyzeAsync(string path, CancellationToken ct)
    {
        // If it's a specific project file, return just that
        if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && this.fileSystem.File.Exists(path))
        {
            return new[] { path };
        }

        // If it's a solution file, find all projects in it
        if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) && this.fileSystem.File.Exists(path))
        {
            return await this.ExtractProjectsFromSolutionAsync(path, ct);
        }

        // Otherwise, search for projects in the directory
        if (this.fileSystem.Directory.Exists(path))
        {
            return await this.projectAnalyzer.FindProjectsAsync(path, ct);
        }

        return Array.Empty<string>();
    }

    private async Task<IReadOnlyList<string>> ExtractProjectsFromSolutionAsync(string solutionPath, CancellationToken ct)
    {
        var projects = new List<string>();
        string solutionDir = this.fileSystem.Path.GetDirectoryName(solutionPath) ?? ".";

        try
        {
            string[] lines = await this.fileSystem.File.ReadAllLinesAsync(solutionPath, ct);

            foreach (string line in lines)
            {
                // Match project lines in .sln file
                // Format: Project("{GUID}") = "Name", "Relative\Path.csproj", "{GUID}"
                if (line.TrimStart().StartsWith("Project(", StringComparison.Ordinal))
                {
                    var match = Regex.Match(line, "\"([^\"]+\\.csproj)\"");
                    if (match.Success)
                    {
                        string relativePath = match.Groups[1].Value;
                        string fullPath = this.fileSystem.Path.Combine(solutionDir, relativePath);
                        fullPath = this.fileSystem.Path.GetFullPath(fullPath);

                        if (this.fileSystem.File.Exists(fullPath))
                        {
                            projects.Add(fullPath);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Warning: Could not parse solution file: {ex.Message}");
        }

        return projects;
    }

    private async Task AnalyzeProjectAsync(
        string projectPath,
        AnalysisOptions options,
        AnalysisResult result,
        CancellationToken ct)
    {
        var projectDir = this.fileSystem.Path.GetDirectoryName(projectPath) ?? ".";
        var projectName = this.fileSystem.Path.GetFileNameWithoutExtension(projectPath);

        if (options.Verbose)
        {
            await Console.Out.WriteLineAsync($"  Analyzing {projectName}...");
        }

        // Build arguments for dotnet build
        var arguments = new List<string>
        {
            "build",
            projectPath,
            "--verbosity", "quiet",
            "-p:TreatWarningsAsErrors=false",
            "-p:WarningLevel=9999", // Show all warnings
        };

        // Ensure analyzers are enabled
        arguments.Add("-p:RunAnalyzers=true");
        arguments.Add("-p:RunAnalyzersDuringBuild=true");

        // Enable additional analyzers if available
        if (options.RunStyleCop)
        {
            arguments.Add("-p:StyleCopEnabled=true");
        }

        if (options.RunSonar)
        {
            arguments.Add("-p:SonarQubeExclude=false");
        }

        // Capture both stdout and stderr
        var stdOutBuilder = new StringBuilder();
        var stdErrBuilder = new StringBuilder();

        var cmd = Cli.Wrap("dotnet")
            .WithArguments(arguments)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuilder))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuilder))
            .WithWorkingDirectory(projectDir);

        try
        {
            await cmd.ExecuteAsync(ct);
        }
        catch (Exception ex)
        {
            // Build may fail, but we still want to capture the output for analysis
            // Log at debug level since build failures are expected when analyzers find issues
            if (options.Verbose)
            {
                await Console.Error.WriteLineAsync($"Build process exited with: {ex.Message}");
            }
        }

        // Parse output for issues
        string output = stdOutBuilder.ToString() + stdErrBuilder.ToString();
        await this.ParseBuildOutputAsync(output, projectPath, options, result, ct);
    }

    private async Task ParseBuildOutputAsync(
        string output,
        string projectPath,
        AnalysisOptions options,
        AnalysisResult result,
        CancellationToken ct)
    {
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string projectDir = this.fileSystem.Path.GetDirectoryName(projectPath) ?? ".";

        foreach (string line in lines)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var issue = this.ParseIssueLine(line, projectDir);

            if (issue != null && ShouldIncludeIssue(issue, options.MinimumSeverity))
            {
                result.Issues.Add(issue);

                if (!result.IssuesByAnalyzer.ContainsKey(issue.Analyzer))
                {
                    result.IssuesByAnalyzer[issue.Analyzer] = new List<AnalysisIssue>();
                }

                result.IssuesByAnalyzer[issue.Analyzer].Add(issue);
            }
        }

        await Task.CompletedTask;
    }

    private AnalysisIssue? ParseIssueLine(string line, string basePath)
    {
        var match = MsBuildPattern.Match(line);
        if (!match.Success)
        {
            return null;
        }

        string filePath = match.Groups[1].Value.Trim();
        string lineNumStr = match.Groups[2].Value;
        string colNumStr = match.Groups[3].Value;
        string severityStr = match.Groups[4].Value.ToLowerInvariant();
        string code = match.Groups[5].Value;
        string message = match.Groups[6].Value.Trim();

        // Determine analyzer type from code
        string analyzer = DetermineAnalyzerType(code, message);

        // Resolve relative paths
        if (!this.fileSystem.Path.IsPathRooted(filePath))
        {
            filePath = this.fileSystem.Path.Combine(basePath, filePath);
        }

        // Parse severity
        AnalysisSeverity severity = severityStr switch
        {
            "error" => AnalysisSeverity.Error,
            "warning" => AnalysisSeverity.Warning,
            _ => AnalysisSeverity.Info,
        };

        // Build help URL
        string? helpUrl = BuildHelpUrl(code, analyzer);

        return new AnalysisIssue
        {
            Analyzer = analyzer,
            RuleId = code,
            Message = message,
            FilePath = filePath,
            LineNumber = int.TryParse(lineNumStr, out int lineNum) ? lineNum : 0,
            ColumnNumber = int.TryParse(colNumStr, out int colNum) ? colNum : 0,
            Severity = severity,
            HelpUrl = helpUrl,
        };
    }

    private static string DetermineAnalyzerType(string code, string message)
    {
        // Roslyn compiler warnings (CSxxxx)
        if (code.StartsWith("CS", StringComparison.OrdinalIgnoreCase))
        {
            return "Roslyn";
        }

        // Roslyn analyzer warnings (CAxxxx)
        if (code.StartsWith("CA", StringComparison.OrdinalIgnoreCase))
        {
            return "Roslyn Analyzers";
        }

        // StyleCop (SAxxxx)
        if (code.StartsWith("SA", StringComparison.OrdinalIgnoreCase))
        {
            return "StyleCop";
        }

        // Sonar (Sxxxx)
        if (code.StartsWith("S", StringComparison.OrdinalIgnoreCase) &&
            code.Length >= 2 && char.IsDigit(code[1]))
        {
            return "Sonar";
        }

        // Roslynator (ROSxxxx, RCSxxxx)
        if (code.StartsWith("ROS", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("RCS", StringComparison.OrdinalIgnoreCase))
        {
            return "Roslynator";
        }

        // Check message for StyleCop indicators
        if (StyleCopPattern.IsMatch(message))
        {
            return "StyleCop";
        }

        // Check message for Sonar indicators
        if (SonarPattern.IsMatch(message))
        {
            return "Sonar";
        }

        // Default to Roslyn for unknown codes
        return "Roslyn";
    }

    private static string? BuildHelpUrl(string code, string analyzer)
    {
        return analyzer switch
        {
            "Roslyn" or "Roslyn Analyzers" =>
                $"https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/{code.ToLowerInvariant()}",
            "StyleCop" =>
                $"https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/{code}.md",
            "Sonar" =>
                $"https://rules.sonarsource.com/csharp/RSPEC-{code[1..]}",
            "Roslynator" =>
                $"https://josefpihrt.github.io/docs/roslynator/analyzers/{code}",
            _ => null,
        };
    }

    private static bool ShouldIncludeIssue(AnalysisIssue issue, AnalysisSeverity minimumSeverity)
    {
        return issue.Severity >= minimumSeverity;
    }

    private static string ResolveProjectPath(string path)
    {
        string fullPath = Path.GetFullPath(path);

        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            return fullPath;
        }

        // Try with .csproj extension
        if (File.Exists(fullPath + ".csproj"))
        {
            return fullPath + ".csproj";
        }

        // Try with .sln extension
        if (File.Exists(fullPath + ".sln"))
        {
            return fullPath + ".sln";
        }

        return fullPath;
    }
}
