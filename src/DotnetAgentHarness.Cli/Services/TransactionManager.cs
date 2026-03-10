namespace DotnetAgentHarness.Cli.Services;

using System.IO.Abstractions;

/// <summary>
/// Manages backup and restore transactions using abstracted file system.
/// </summary>
public class TransactionManager : ITransactionManager
{
    private readonly IFileSystem fileSystem;

    /// <summary>
    /// Creates a new TransactionManager instance.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    public TransactionManager(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    /// <inheritdoc />
    public async Task<string> BackupAsync(string path)
    {
        string rulesyncPath = this.fileSystem.Path.Combine(path, ".rulesync");

        if (!this.fileSystem.Directory.Exists(rulesyncPath))
        {
            return string.Empty;
        }

        string backupPath = this.fileSystem.Path.Combine(
            this.fileSystem.Path.GetTempPath(),
            $"rulesync-backup-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}");

        await Task.Run(() =>
        {
            this.fileSystem.Directory.CreateDirectory(backupPath);
            CopyDirectory(rulesyncPath, this.fileSystem.Path.Combine(backupPath, ".rulesync"), true);
        });

        return backupPath;
    }

    /// <inheritdoc />
    public async Task RestoreAsync(string backupPath, string targetPath)
    {
        if (!this.fileSystem.Directory.Exists(backupPath))
        {
            throw new DirectoryNotFoundException($"Backup not found: {backupPath}");
        }

        string rulesyncPath = this.fileSystem.Path.Combine(targetPath, ".rulesync");
        string backupRulesync = this.fileSystem.Path.Combine(backupPath, ".rulesync");

        await Task.Run(() =>
        {
            // Remove existing .rulesync if present
            if (this.fileSystem.Directory.Exists(rulesyncPath))
            {
                this.fileSystem.Directory.Delete(rulesyncPath, true);
            }

            // Restore from backup
            if (this.fileSystem.Directory.Exists(backupRulesync))
            {
                CopyDirectory(backupRulesync, rulesyncPath, true);
            }
        });
    }

    /// <inheritdoc />
    public async Task CleanupAsync(string backupPath)
    {
        if (!this.fileSystem.Directory.Exists(backupPath))
        {
            return;
        }

        await Task.Run(() =>
        {
            this.fileSystem.Directory.Delete(backupPath, true);
        });
    }

    private void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        IDirectoryInfo dir = this.fileSystem.DirectoryInfo.New(sourceDir);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        // Create destination directory
        this.fileSystem.Directory.CreateDirectory(destinationDir);

        // Get the files in the directory and copy them to the new location
        foreach (IFileInfo file in dir.GetFiles())
        {
            string targetFilePath = this.fileSystem.Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (IDirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = this.fileSystem.Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }
}

/// <summary>
/// Interface for managing backup and restore transactions.
/// </summary>
public interface ITransactionManager
{
    /// <summary>
    /// Creates a backup of the .rulesync directory.
    /// </summary>
    /// <param name="path">The path containing .rulesync.</param>
    /// <returns>The backup directory path.</returns>
    Task<string> BackupAsync(string path);

    /// <summary>
    /// Restores from a backup.
    /// </summary>
    /// <param name="backupPath">The backup directory path.</param>
    /// <param name="targetPath">The target path to restore to.</param>
    /// <returns>Task.</returns>
    Task RestoreAsync(string backupPath, string targetPath);

    /// <summary>
    /// Cleans up a backup directory.
    /// </summary>
    /// <param name="backupPath">The backup directory path.</param>
    /// <returns>Task.</returns>
    Task CleanupAsync(string backupPath);
}
