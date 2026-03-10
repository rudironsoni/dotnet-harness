namespace DotnetAgentHarness.Cli.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;

/// <summary>
/// Downloads hooks from GitHub with resilience policies.
/// </summary>
public class HookDownloader : IHookDownloader
{
    private readonly HttpClient httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> retryPolicy;

    /// <summary>
    /// Creates a new HookDownloader instance.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    public HookDownloader(HttpClient httpClient)
    {
        this.httpClient = httpClient;

        // Configure retry policy with exponential backoff
        this.retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.Error.WriteLine($"Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                });
    }

    /// <inheritdoc />
    public async Task<HookDownloadResult> DownloadHooksAsync(
        string[] hookScripts,
        string source,
        string installPath)
    {
        string hooksDir = Path.Combine(installPath, ".rulesync", "hooks");
        Directory.CreateDirectory(hooksDir);

        List<string> downloadedHooks = new();

        foreach (string hook in hookScripts)
        {
            try
            {
                string url = $"https://raw.githubusercontent.com/{source}/main/hooks/{hook}";

                // Execute with retry policy
                HttpResponseMessage response = await this.retryPolicy.ExecuteAsync(
                    async ct => await this.httpClient.GetAsync(url, ct),
                    CancellationToken.None);

                if (!response.IsSuccessStatusCode)
                {
                    return new HookDownloadResult(
                        false,
                        Array.Empty<string>(),
                        $"Failed to download {hook}: {response.StatusCode}");
                }

                string content = await response.Content.ReadAsStringAsync();
                string hookPath = Path.Combine(hooksDir, hook);
                await File.WriteAllTextAsync(hookPath, content);
                downloadedHooks.Add(hook);
            }
            catch (Exception ex)
            {
                return new HookDownloadResult(
                    false,
                    Array.Empty<string>(),
                    $"Failed to download {hook}: {ex.Message}");
            }
        }

        return new HookDownloadResult(true, downloadedHooks.ToArray(), string.Empty);
    }

    /// <inheritdoc />
    public async Task<GitHubRelease?> GetLatestReleaseAsync(string repo)
    {
        try
        {
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            this.httpClient.DefaultRequestHeaders.Add("User-Agent", "dotnet-agent-harness");

            // Execute with retry policy
            HttpResponseMessage response = await this.retryPolicy.ExecuteAsync(
                async ct => await this.httpClient.GetAsync(
                    $"https://api.github.com/repos/{repo}/releases/latest", ct),
                CancellationToken.None);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize(content, GitHubJsonContext.Default.GitHubRelease);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// GitHub release information.
/// </summary>
/// <param name="TagName">The release tag name.</param>
/// <param name="HtmlUrl">The URL to the release page.</param>
/// <param name="PublishedAt">The publication date.</param>
public sealed record GitHubRelease(string TagName, string HtmlUrl, string PublishedAt);

/// <summary>
/// JSON serialization context for GitHub API responses.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GitHubRelease))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Interface for downloading hooks from remote sources.
/// </summary>
public interface IHookDownloader
{
    /// <summary>
    /// Downloads hook scripts from a GitHub repository.
    /// </summary>
    /// <param name="hookScripts">Array of hook script names.</param>
    /// <param name="source">Repository source in format owner/repo.</param>
    /// <param name="installPath">Local path to install hooks.</param>
    /// <returns>Download result.</returns>
    Task<HookDownloadResult> DownloadHooksAsync(string[] hookScripts, string source, string installPath);

    /// <summary>
    /// Gets the latest release from a GitHub repository.
    /// </summary>
    /// <param name="repo">Repository in format owner/repo.</param>
    /// <returns>Latest release info or null.</returns>
    Task<GitHubRelease?> GetLatestReleaseAsync(string repo);
}
