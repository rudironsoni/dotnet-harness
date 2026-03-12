namespace DotnetAgentHarness.Cli.Services;

public class PrerequisiteChecker : IPrerequisiteChecker
{
    public Task<PrerequisiteResult> CheckAsync()
    {
        // SDK is self-contained, no external binary needed
        // Return success with SDK version
        return Task.FromResult(new PrerequisiteResult(true, "7.18.1-rc.1"));
    }
}

public interface IPrerequisiteChecker
{
    Task<PrerequisiteResult> CheckAsync();
}

public sealed record PrerequisiteResult(
    bool Success,
    string RulesyncVersion,
    string? ErrorMessage = null);
