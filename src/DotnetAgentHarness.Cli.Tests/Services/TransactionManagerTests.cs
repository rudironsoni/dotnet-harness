namespace DotnetAgentHarness.Cli.Tests.Services;

using DotnetAgentHarness.Cli.Services;
using Xunit;

public class TransactionManagerTests : IDisposable
{
    private readonly string testDir;
    private readonly TransactionManager manager;
    private bool disposedValue;

    public TransactionManagerTests()
    {
        this.testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this.testDir);
        this.manager = new TransactionManager();
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task BackupAsync_WithNoRulesync_ReturnsEmptyString()
    {
        // Act
        string result = await this.manager.BackupAsync(this.testDir);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task BackupAsync_WithRulesync_CreatesBackup()
    {
        // Arrange
        string rulesyncDir = Path.Combine(this.testDir, ".rulesync");
        Directory.CreateDirectory(rulesyncDir);
        await File.WriteAllTextAsync(Path.Combine(rulesyncDir, "test.txt"), "test");

        // Act
        string result = await this.manager.BackupAsync(this.testDir);

        // Assert
        Assert.NotEqual(string.Empty, result);
        Assert.True(Directory.Exists(result));

        // Cleanup
        if (Directory.Exists(result))
        {
            Directory.Delete(result, true);
        }
    }

    [Fact]
    public async Task RestoreAsync_WithValidBackup_RestoresFiles()
    {
        // Arrange
        string rulesyncDir = Path.Combine(this.testDir, ".rulesync");
        Directory.CreateDirectory(rulesyncDir);
        await File.WriteAllTextAsync(Path.Combine(rulesyncDir, "test.txt"), "test");

        string backupPath = await this.manager.BackupAsync(this.testDir);
        Assert.NotEqual(string.Empty, backupPath);

        // Delete original
        Directory.Delete(rulesyncDir, true);

        // Act
        await this.manager.RestoreAsync(backupPath);

        // Assert
        Assert.True(Directory.Exists(rulesyncDir));
        Assert.Equal("test", await File.ReadAllTextAsync(Path.Combine(rulesyncDir, "test.txt")));

        // Cleanup
        if (Directory.Exists(backupPath))
        {
            Directory.Delete(backupPath, true);
        }
    }

    [Fact]
    public async Task CleanupAsync_WithValidBackup_RemovesBackup()
    {
        // Arrange
        string rulesyncDir = Path.Combine(this.testDir, ".rulesync");
        Directory.CreateDirectory(rulesyncDir);
        await File.WriteAllTextAsync(Path.Combine(rulesyncDir, "test.txt"), "test");

        string backupPath = await this.manager.BackupAsync(this.testDir);
        Assert.NotEqual(string.Empty, backupPath);

        // Act
        await this.manager.CleanupAsync(backupPath);

        // Assert
        Assert.False(Directory.Exists(backupPath));
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
