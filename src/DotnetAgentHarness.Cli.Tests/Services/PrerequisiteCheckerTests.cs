namespace DotnetAgentHarness.Cli.Tests.Services;

using DotnetAgentHarness.Cli.Services;
using Xunit;

public class PrerequisiteCheckerTests
{
    [Fact]
    public async Task CheckAsync_WhenRulesyncInstalled_ReturnsSuccess()
    {
        // Arrange
        PrerequisiteChecker checker = new();

        // Act
        PrerequisiteResult result = await checker.CheckAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal("7.18.1-rc.1", result.RulesyncVersion);
    }

    [Fact]
    public async Task CheckAsync_Always_ReturnsSuccess()
    {
        // Arrange
        PrerequisiteChecker checker = new();

        // Act
        PrerequisiteResult result = await checker.CheckAsync();

        // Assert
        // SDK is self-contained, so this should always succeed
        Assert.True(result.Success);
        Assert.NotNull(result.RulesyncVersion);
        Assert.NotEmpty(result.RulesyncVersion);
    }
}
