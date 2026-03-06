using System.IO;
using System.Text.Json;
using DotNetAgentHarness.Evals.Models;

namespace DotNetAgentHarness.Evals.Engine;

public static class EvalArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string Write(string artifactPath, EvalRunArtifact artifact)
    {
        var fullPath = Path.GetFullPath(artifactPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, JsonSerializer.Serialize(artifact, JsonOptions));
        return fullPath;
    }
}
