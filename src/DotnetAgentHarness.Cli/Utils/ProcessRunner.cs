namespace DotnetAgentHarness.Cli.Utils;

using CliWrap;
using CliWrap.Buffered;

/// <summary>
/// Process runner using CliWrap for reliable async process execution.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(
        string command,
        string arguments,
        string? workingDirectory = null)
    {
        Command cmd = Cli.Wrap(command)
            .WithArguments(arguments);

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            cmd = cmd.WithWorkingDirectory(workingDirectory);
        }

        BufferedCommandResult result = await cmd.ExecuteBufferedAsync();

        return new ProcessResult(
            result.ExitCode,
            result.StandardOutput.TrimEnd(),
            result.StandardError.TrimEnd());
    }
}

/// <summary>
/// Interface for running external processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs a process asynchronously.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">The arguments to pass.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <returns>The process result.</returns>
    Task<ProcessResult> RunAsync(string command, string arguments, string? workingDirectory = null);
}
