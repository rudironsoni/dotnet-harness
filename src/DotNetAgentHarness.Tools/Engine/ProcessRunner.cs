using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace DotNetAgentHarness.Tools.Engine;

public static class ProcessRunner
{
    public static void Run(string fileName, string arguments, string workingDirectory, int timeoutMs = 60_000)
    {
        var result = Run(fileName, arguments, workingDirectory, timeoutMs, throwOnError: false);
        if (result.TimedOut)
        {
            throw new InvalidOperationException(
                $"Command '{fileName} {arguments}' timed out after {timeoutMs}ms:{Environment.NewLine}{FormatOutput(result)}");
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command '{fileName} {arguments}' failed with exit code {result.ExitCode}:{Environment.NewLine}{FormatOutput(result)}");
        }
    }

    public static ProcessExecutionResult Run(string fileName, string arguments, string workingDirectory, int timeoutMs, bool throwOnError)
    {
        var startInfo = CreateStartInfo(fileName, workingDirectory);
        startInfo.Arguments = arguments;
        var result = Run(startInfo, timeoutMs);

        if (throwOnError)
        {
            if (result.TimedOut)
            {
                throw new InvalidOperationException(
                    $"Command '{fileName} {arguments}' timed out after {timeoutMs}ms:{Environment.NewLine}{FormatOutput(result)}");
            }

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Command '{fileName} {arguments}' failed with exit code {result.ExitCode}:{Environment.NewLine}{FormatOutput(result)}");
            }
        }

        return result;
    }

    public static ProcessExecutionResult RunShell(string command, string workingDirectory, int timeoutMs = 60_000)
    {
        var startInfo = CreateShellStartInfo(command, workingDirectory);
        return Run(startInfo, timeoutMs);
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, string workingDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static ProcessStartInfo CreateShellStartInfo(string command, string workingDirectory)
    {
        var startInfo = OperatingSystem.IsWindows()
            ? CreateStartInfo("cmd.exe", workingDirectory)
            : CreateStartInfo("/bin/sh", workingDirectory);

        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command);
        }
        else
        {
            startInfo.ArgumentList.Add("-lc");
            startInfo.ArgumentList.Add(command);
        }

        return startInfo;
    }

    private static ProcessExecutionResult Run(ProcessStartInfo startInfo, int timeoutMs)
    {
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{startInfo.FileName}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            TryKill(process);
            Task.WaitAll(stdoutTask, stderrTask);
            return new ProcessExecutionResult
            {
                Command = BuildCommandLine(startInfo),
                ExitCode = -1,
                StandardOutput = stdoutTask.Result,
                StandardError = stderrTask.Result,
                TimedOut = true
            };
        }

        Task.WaitAll(stdoutTask, stderrTask);
        return new ProcessExecutionResult
        {
            Command = BuildCommandLine(startInfo),
            ExitCode = process.ExitCode,
            StandardOutput = stdoutTask.Result,
            StandardError = stderrTask.Result
        };
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string BuildCommandLine(ProcessStartInfo startInfo)
    {
        var builder = new StringBuilder(startInfo.FileName);
        if (startInfo.ArgumentList.Count > 0)
        {
            foreach (var argument in startInfo.ArgumentList)
            {
                builder.Append(' ').Append(argument);
            }
        }
        else if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
        {
            builder.Append(' ').Append(startInfo.Arguments);
        }

        return builder.ToString();
    }

    private static string FormatOutput(ProcessExecutionResult result)
    {
        var output = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            output.AppendLine(result.StandardOutput.Trim());
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            output.AppendLine(result.StandardError.Trim());
        }

        return output.ToString().Trim();
    }
}
