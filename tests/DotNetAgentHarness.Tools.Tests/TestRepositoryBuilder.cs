using System;
using System.IO;

namespace DotNetAgentHarness.Tools.Tests;

internal sealed class TestRepositoryBuilder : IDisposable
{
    public TestRepositoryBuilder()
    {
        Root = Path.Combine(Path.GetTempPath(), "dotnet-agent-harness-tools-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
