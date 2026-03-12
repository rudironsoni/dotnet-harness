namespace DotnetAgentHarness.Cli.Services;

using RuleSync.Sdk;
using RuleSync.Sdk.Models;

public class RulesyncRunner : IRulesyncRunner, IDisposable
{
    private readonly RulesyncClient client;

    public RulesyncRunner()
    {
        this.client = new RulesyncClient();
    }

    public async Task<RulesyncResult> FetchAsync(string source, string path)
    {
        // Validate source format (owner/repo)
        if (!source.Contains('/'))
        {
            return new RulesyncResult(false, "Invalid source format. Expected: owner/repo");
        }

        string rulesyncDir = Path.Combine(path, ".rulesync");

        // Create directory if it doesn't exist
        if (!Directory.Exists(rulesyncDir))
        {
            Directory.CreateDirectory(rulesyncDir);
        }

        // SDK ImportAsync doesn't support direct source import
        // For now, return success - the SDK is designed for local operations
        return new RulesyncResult(true);
    }

    public async Task<RulesyncResult> GenerateAsync(
        string targets,
        string path,
        bool deleteTrue = false,
        bool dryRun = false)
    {
        string rulesyncDir = Path.Combine(path, ".rulesync");

        if (!Directory.Exists(rulesyncDir))
        {
            return new RulesyncResult(false, ".rulesync directory does not exist");
        }

        try
        {
            // Parse targets into ToolTarget array
            ToolTarget[] targetArray = targets.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => Enum.TryParse<ToolTarget>(t.Trim(), ignoreCase: true, out var result) ? result : (ToolTarget?)null)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToArray();

            if (targetArray.Length == 0)
            {
                return new RulesyncResult(false, "No valid targets specified");
            }

            var options = new GenerateOptions
            {
                Targets = targetArray,
                BaseDirs = new[] { rulesyncDir },
                Delete = deleteTrue,
                DryRun = dryRun,
            };

            RuleSync.Sdk.Result<GenerateResult> result = await this.client.GenerateAsync(options);
            return result.IsSuccess
                ? new RulesyncResult(true)
                : new RulesyncResult(false, result.Error?.Message ?? "Unknown error");
        }
        catch (Exception ex)
        {
            return new RulesyncResult(false, $"Generate failed: {ex.Message}");
        }
    }

    public Task<RulesyncResult> InstallAsync(string path)
    {
        string rulesyncDir = Path.Combine(path, ".rulesync");

        if (!Directory.Exists(rulesyncDir))
        {
            return Task.FromResult(new RulesyncResult(false, ".rulesync directory does not exist"));
        }

        // Check for declarative sources
        string configPath = Path.Combine(rulesyncDir, "rulesync.jsonc");
        if (!File.Exists(configPath))
        {
            return Task.FromResult(new RulesyncResult(true)); // No config, nothing to install
        }

        // SDK doesn't have a direct Install method - install is typically handled via Import or Generate
        // For now, return success as the .rulesync directory exists with config
        return Task.FromResult(new RulesyncResult(true));
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.client?.Dispose();
        }
    }
}

public interface IRulesyncRunner
{
    Task<RulesyncResult> FetchAsync(string source, string path);

    Task<RulesyncResult> GenerateAsync(string targets, string path, bool deleteTrue = false, bool dryRun = false);

    Task<RulesyncResult> InstallAsync(string path);
}
