namespace DotnetAgentHarness.Cli.Tests.Services;

using DotnetAgentHarness.Cli.Services;
using DotnetAgentHarness.Cli.Utils;
using NSubstitute;
using Xunit;

public class RulesyncRunnerTests : IDisposable
{
    private readonly string testDir;
    private bool disposedValue;

    public RulesyncRunnerTests()
    {
        this.testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this.testDir);
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task FetchAsync_WithInvalidSource_ReturnsFailure()
    {
        // Arrange
        IProcessRunner processRunner = Substitute.For<IProcessRunner>();
        RulesyncRunner runner = new(processRunner);

        // Act
        RulesyncResult result = await runner.FetchAsync("invalid-source", this.testDir);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid source format", result.Error);
    }

    [Fact]
    public async Task GenerateAsync_WhenRulesyncNotExists_ReturnsFailure()
    {
        // Arrange
        IProcessRunner processRunner = Substitute.For<IProcessRunner>();
        RulesyncRunner runner = new(processRunner);

        // Act
        RulesyncResult result = await runner.GenerateAsync("claudecode", this.testDir);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(".rulesync directory does not exist", result.Error);
    }

    [Fact]
    public async Task InstallAsync_WhenRulesyncNotExists_ReturnsFailure()
    {
        // Arrange
        IProcessRunner processRunner = Substitute.For<IProcessRunner>();
        RulesyncRunner runner = new(processRunner);

        // Act
        RulesyncResult result = await runner.InstallAsync(this.testDir);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(".rulesync directory does not exist", result.Error);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this.disposedValue)
        {
            return;
        }

        if (disposing && Directory.Exists(this.testDir))
        {
            Directory.Delete(this.testDir, true);
        }

        this.disposedValue = true;
    }
}
