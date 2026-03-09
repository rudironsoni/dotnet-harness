namespace DotnetAgentHarness.Cli.Tests.Services;

using System.Text.Json;
using DotnetAgentHarness.Cli.Services;
using Xunit;

public class ConfigDetectorTests : IDisposable
{
    private readonly string testDir;
    private readonly ConfigDetector detector;
    private bool disposedValue;

    public ConfigDetectorTests()
    {
        this.testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this.testDir);
        this.detector = new ConfigDetector();
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task HasDeleteTrueAsync_WhenConfigHasDeleteTrue_ReturnsTrue()
    {
        // Arrange
        string rulesyncDir = Path.Combine(this.testDir, ".rulesync");
        Directory.CreateDirectory(rulesyncDir);

        string configContent = @"{
            ""delete"": true,
            ""sources"": []
        }";
        await File.WriteAllTextAsync(Path.Combine(rulesyncDir, "rulesync.jsonc"), configContent);

        // Act
        bool result = await this.detector.HasDeleteTrueAsync(this.testDir);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasDeleteTrueAsync_WhenConfigHasDeleteFalse_ReturnsFalse()
    {
        // Arrange
        string rulesyncDir = Path.Combine(this.testDir, ".rulesync");
        Directory.CreateDirectory(rulesyncDir);

        string configContent = @"{
            ""delete"": false,
            ""sources"": []
        }";
        await File.WriteAllTextAsync(Path.Combine(rulesyncDir, "rulesync.jsonc"), configContent);

        // Act
        bool result = await this.detector.HasDeleteTrueAsync(this.testDir);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasDeleteTrueAsync_WhenConfigDoesNotExist_ReturnsFalse()
    {
        // Act
        bool result = await this.detector.HasDeleteTrueAsync(this.testDir);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasDeclarativeSourcesAsync_WhenConfigHasSources_ReturnsTrue()
    {
        // Arrange
        string rulesyncDir = Path.Combine(this.testDir, ".rulesync");
        Directory.CreateDirectory(rulesyncDir);

        string configContent = @"{
            ""sources"": [
                { ""source"": ""owner/repo"" }
            ]
        }";
        await File.WriteAllTextAsync(Path.Combine(rulesyncDir, "rulesync.jsonc"), configContent);

        // Act
        bool result = await this.detector.HasDeclarativeSourcesAsync(this.testDir);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetTargetPlatformsAsync_WhenConfigHasTargets_ReturnsTargets()
    {
        // Arrange
        string rulesyncDir = Path.Combine(this.testDir, ".rulesync");
        Directory.CreateDirectory(rulesyncDir);

        string configContent = @"{
            ""targets"": [""claudecode"", ""copilot""]
        }";
        await File.WriteAllTextAsync(Path.Combine(rulesyncDir, "rulesync.jsonc"), configContent);

        // Act
        string[] result = await this.detector.GetTargetPlatformsAsync(this.testDir);

        // Assert
        Assert.Equal(new[] { "claudecode", "copilot" }, result);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                if (Directory.Exists(this.testDir))
                {
                    Directory.Delete(this.testDir, true);
                }
            }

            this.disposedValue = true;
        }
    }
}
