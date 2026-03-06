using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DotNetAgentHarness.Tools.Engine;

public static class ReviewEngine
{
    private static readonly string[] IgnoredDirectoryNames =
    {
        ".git",
        ".tmp",
        "bin",
        "obj",
        "node_modules",
        "dist",
        ".rulesync",
        ".codex",
        ".claude",
        ".opencode"
    };

    private static readonly ReviewRule[] Rules =
    {
        new("sync-over-async", "warning", ".cs", new Regex(@"\.Result\b|\.Wait\s*\(|GetAwaiter\(\)\.GetResult\(\)", RegexOptions.Compiled), "Blocking async calls detected.", "Prefer `await` end-to-end or isolate sync boundaries explicitly.", IgnoreCommentsAndStrings: true),
        new("build-service-provider", "warning", ".cs", new Regex(@"BuildServiceProvider\s*\(", RegexOptions.Compiled), "Manual DI container construction detected.", "Avoid `BuildServiceProvider()` in app code. Resolve through constructor injection.", IgnoreCommentsAndStrings: true),
        new("new-httpclient", "suggestion", ".cs", new Regex(@"new\s+HttpClient\s*\(", RegexOptions.Compiled), "Direct `HttpClient` construction detected.", "Prefer `IHttpClientFactory` unless there is a documented lifetime reason not to.", IgnoreCommentsAndStrings: true),
        new("task-run", "suggestion", ".cs", new Regex(@"Task\.Run\s*\(", RegexOptions.Compiled), "Background work offloading detected.", "Check whether `Task.Run` is masking async design issues or request-thread work.", IgnoreCommentsAndStrings: true),
        new("pragma-warning-disable", "warning", ".cs", new Regex(@"#pragma\s+warning\s+disable", RegexOptions.Compiled), "Warnings are being suppressed in code.", "Verify the suppression is scoped, justified, and not hiding a real defect."),
        new("catch-general-exception", "warning", ".cs", new Regex(@"catch\s*\(\s*Exception(?:\s+\w+)?\s*\)", RegexOptions.Compiled), "General exception catch detected.", "Catch specific exception types unless the boundary is explicitly translating failures.", IgnoreCommentsAndStrings: true),
        new("empty-catch-block", "warning", ".cs", new Regex(@"catch\s*(?:\(\s*[^)]*\))?\s*\{\s*\}", RegexOptions.Compiled | RegexOptions.Singleline), "Empty catch block detected.", "Do not silently swallow failures. Log, translate, or rethrow explicitly.", IgnoreCommentsAndStrings: true),
        new("throw-ex", "warning", ".cs", new Regex(@"throw\s+ex\s*;", RegexOptions.Compiled), "Stack trace resetting rethrow detected.", "Use `throw;` to preserve the original stack trace.", IgnoreCommentsAndStrings: true),
        new("count-greater-than-zero", "suggestion", ".cs", new Regex(@"\.Count\s*\(\s*\)\s*>\s*0", RegexOptions.Compiled), "Count-based existence check detected.", "Prefer `.Any()` for existence checks so enumeration can short-circuit.", IgnoreCommentsAndStrings: true),
        new("no-warn", "warning", ".csproj", new Regex(@"<NoWarn>.*?</NoWarn>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline), "Project-wide warning suppression detected.", "Review whether `NoWarn` is hiding actionable compiler or analyzer diagnostics."),
        new("no-warn-props", "warning", ".props", new Regex(@"<NoWarn>.*?</NoWarn>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline), "Shared warning suppression detected.", "Review whether shared `NoWarn` settings are suppressing actionable diagnostics."),
        new("preview-langversion", "suggestion", ".csproj", new Regex(@"<LangVersion>\s*preview\s*</LangVersion>", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Preview language version enabled.", "Ensure preview features are intentional and supported by the team and CI."),
        new("preview-langversion-props", "suggestion", ".props", new Regex(@"<LangVersion>\s*preview\s*</LangVersion>", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Preview language version enabled.", "Ensure preview features are intentional and supported by the team and CI."),
        new("preview-features", "suggestion", ".csproj", new Regex(@"<EnablePreviewFeatures>\s*true\s*</EnablePreviewFeatures>", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Preview framework features enabled.", "Confirm preview APIs are intentionally enabled and pinned via SDK versioning."),
        new("preview-features-props", "suggestion", ".props", new Regex(@"<EnablePreviewFeatures>\s*true\s*</EnablePreviewFeatures>", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Preview framework features enabled.", "Confirm preview APIs are intentionally enabled and pinned via SDK versioning.")
    };

    public static ReviewReport Review(string repoRoot, string? relativePath = null, int limit = 100)
    {
        var root = string.IsNullOrWhiteSpace(relativePath)
            ? Path.GetFullPath(repoRoot)
            : Path.GetFullPath(Path.Combine(repoRoot, relativePath));
        var findings = new List<ReviewFinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scannedFiles = 0;

        foreach (var filePath in EnumerateFiles(root))
        {
            scannedFiles++;
            AnalyzeFile(repoRoot, filePath, findings, seen, limit);
            if (findings.Count >= limit)
            {
                break;
            }
        }

        return new ReviewReport
        {
            RepoRoot = Path.GetFullPath(repoRoot),
            ScannedFiles = scannedFiles,
            Findings = findings
        };
    }

    private static void AnalyzeFile(string repoRoot, string filePath, List<ReviewFinding> findings, HashSet<string> seen, int limit)
    {
        var extension = Path.GetExtension(filePath);
        var matchingRules = Rules.Where(rule => rule.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matchingRules.Count == 0)
        {
            return;
        }

        var content = File.ReadAllText(filePath);
        var lineStarts = BuildLineStarts(content);
        var ignoredMask = extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            ? BuildIgnoredMask(content)
            : Array.Empty<bool>();

        foreach (var rule in matchingRules)
        {
            foreach (Match match in rule.Pattern.Matches(content))
            {
                if (!match.Success || match.Length == 0)
                {
                    continue;
                }

                if (rule.IgnoreCommentsAndStrings && OverlapsIgnoredRange(match.Index, match.Length, ignoredMask))
                {
                    continue;
                }

                var lineNumber = GetLineNumber(lineStarts, match.Index);
                var dedupeKey = $"{rule.Id}|{filePath}|{lineNumber}";
                if (!seen.Add(dedupeKey))
                {
                    continue;
                }

                findings.Add(new ReviewFinding
                {
                    Severity = rule.Severity,
                    RuleId = rule.Id,
                    FilePath = Path.GetRelativePath(repoRoot, filePath),
                    LineNumber = lineNumber,
                    Message = rule.Message,
                    Guidance = rule.Guidance,
                    Evidence = GetLineText(content, lineStarts, lineNumber).Trim()
                });

                if (findings.Count >= limit)
                {
                    return;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (File.Exists(current))
            {
                yield return current;
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (ShouldIgnore(directory))
                {
                    continue;
                }

                pending.Push(directory);
            }

            foreach (var filePath in Directory.EnumerateFiles(current))
            {
                if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                    || filePath.EndsWith(".props", StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (!fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
                        && !fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
                        && !fileName.Equals("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return filePath;
                    }
                }
            }
        }
    }

    private static int[] BuildLineStarts(string content)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] == '\n' && index + 1 < content.Length)
            {
                starts.Add(index + 1);
            }
        }

        return starts.ToArray();
    }

    private static int GetLineNumber(int[] lineStarts, int index)
    {
        var line = Array.BinarySearch(lineStarts, index);
        if (line >= 0)
        {
            return line + 1;
        }

        return ~line;
    }

    private static string GetLineText(string content, int[] lineStarts, int lineNumber)
    {
        var lineIndex = Math.Max(0, lineNumber - 1);
        var start = lineStarts[lineIndex];
        var end = lineIndex + 1 < lineStarts.Length ? lineStarts[lineIndex + 1] - 1 : content.Length;
        while (end > start && (content[end - 1] == '\r' || content[end - 1] == '\n'))
        {
            end--;
        }

        return content[start..Math.Max(start, end)];
    }

    private static bool[] BuildIgnoredMask(string content)
    {
        var ignored = new bool[content.Length];
        var index = 0;

        while (index < content.Length)
        {
            if (Matches(content, index, "//"))
            {
                index = MarkSingleLineComment(content, ignored, index);
                continue;
            }

            if (Matches(content, index, "/*"))
            {
                index = MarkBlockComment(content, ignored, index);
                continue;
            }

            if (TryReadStringStart(content, index, out var kind, out var prefixLength, out var quoteCount))
            {
                index = kind switch
                {
                    StringKind.Raw => MarkRawString(content, ignored, index, prefixLength, quoteCount),
                    StringKind.Verbatim => MarkVerbatimString(content, ignored, index, prefixLength),
                    _ => MarkRegularString(content, ignored, index, prefixLength)
                };
                continue;
            }

            if (content[index] == '\'')
            {
                index = MarkCharLiteral(content, ignored, index);
                continue;
            }

            index++;
        }

        return ignored;
    }

    private static bool TryReadStringStart(string content, int index, out StringKind kind, out int prefixLength, out int quoteCount)
    {
        kind = StringKind.None;
        prefixLength = 0;
        quoteCount = 0;

        var cursor = index;
        var seenAt = false;
        while (cursor < content.Length && (content[cursor] == '$' || content[cursor] == '@'))
        {
            if (content[cursor] == '@')
            {
                if (seenAt)
                {
                    return false;
                }

                seenAt = true;
            }

            cursor++;
        }

        if (cursor >= content.Length || content[cursor] != '"')
        {
            return false;
        }

        while (cursor + quoteCount < content.Length && content[cursor + quoteCount] == '"')
        {
            quoteCount++;
        }

        prefixLength = cursor + quoteCount - index;
        kind = quoteCount >= 3
            ? StringKind.Raw
            : seenAt
                ? StringKind.Verbatim
                : StringKind.Regular;

        return true;
    }

    private static int MarkSingleLineComment(string content, bool[] ignored, int start)
    {
        var index = start;
        while (index < content.Length && content[index] != '\n')
        {
            ignored[index] = true;
            index++;
        }

        return index;
    }

    private static int MarkBlockComment(string content, bool[] ignored, int start)
    {
        var index = start;
        while (index < content.Length)
        {
            ignored[index] = true;
            if (index + 1 < content.Length)
            {
                ignored[index + 1] = true;
            }

            if (Matches(content, index, "*/"))
            {
                return index + 2;
            }

            index++;
        }

        return index;
    }

    private static int MarkRegularString(string content, bool[] ignored, int start, int prefixLength)
    {
        var index = start;
        var end = Math.Min(content.Length, start + prefixLength);
        while (index < end)
        {
            ignored[index] = true;
            index++;
        }

        while (index < content.Length)
        {
            ignored[index] = true;
            if (content[index] == '\\' && index + 1 < content.Length)
            {
                ignored[index + 1] = true;
                index += 2;
                continue;
            }

            if (content[index] == '"')
            {
                return index + 1;
            }

            index++;
        }

        return index;
    }

    private static int MarkVerbatimString(string content, bool[] ignored, int start, int prefixLength)
    {
        var index = start;
        var end = Math.Min(content.Length, start + prefixLength);
        while (index < end)
        {
            ignored[index] = true;
            index++;
        }

        while (index < content.Length)
        {
            ignored[index] = true;
            if (content[index] == '"' && index + 1 < content.Length && content[index + 1] == '"')
            {
                ignored[index + 1] = true;
                index += 2;
                continue;
            }

            if (content[index] == '"')
            {
                return index + 1;
            }

            index++;
        }

        return index;
    }

    private static int MarkRawString(string content, bool[] ignored, int start, int prefixLength, int quoteCount)
    {
        var index = start;
        var end = Math.Min(content.Length, start + prefixLength);
        while (index < end)
        {
            ignored[index] = true;
            index++;
        }

        while (index < content.Length)
        {
            ignored[index] = true;
            if (HasQuoteRun(content, index, quoteCount))
            {
                for (var offset = 1; offset < quoteCount && index + offset < content.Length; offset++)
                {
                    ignored[index + offset] = true;
                }

                return index + quoteCount;
            }

            index++;
        }

        return index;
    }

    private static int MarkCharLiteral(string content, bool[] ignored, int start)
    {
        var index = start;
        ignored[index] = true;
        index++;

        while (index < content.Length)
        {
            ignored[index] = true;
            if (content[index] == '\\' && index + 1 < content.Length)
            {
                ignored[index + 1] = true;
                index += 2;
                continue;
            }

            if (content[index] == '\'')
            {
                return index + 1;
            }

            index++;
        }

        return index;
    }

    private static bool OverlapsIgnoredRange(int start, int length, bool[] ignoredMask)
    {
        if (ignoredMask.Length == 0)
        {
            return false;
        }

        var end = Math.Min(ignoredMask.Length, start + length);
        for (var index = start; index < end; index++)
        {
            if (ignoredMask[index])
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasQuoteRun(string content, int index, int count)
    {
        if (index + count > content.Length)
        {
            return false;
        }

        for (var offset = 0; offset < count; offset++)
        {
            if (content[index + offset] != '"')
            {
                return false;
            }
        }

        return true;
    }

    private static bool Matches(string content, int index, string value)
    {
        if (index + value.Length > content.Length)
        {
            return false;
        }

        for (var offset = 0; offset < value.Length; offset++)
        {
            if (content[index + offset] != value[offset])
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldIgnore(string path)
    {
        var directoryName = Path.GetFileName(path);
        return IgnoredDirectoryNames.Contains(directoryName, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record ReviewRule(
        string Id,
        string Severity,
        string Extension,
        Regex Pattern,
        string Message,
        string Guidance,
        bool IgnoreCommentsAndStrings = false);

    private enum StringKind
    {
        None,
        Regular,
        Verbatim,
        Raw
    }
}
