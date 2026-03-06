using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetAgentHarness.Tools.Engine;

public static class PromptBundleRenderer
{
    public static string NormalizePlatform(string? platform)
    {
        var normalized = (platform ?? PromptPlatforms.Generic).Trim().ToLowerInvariant();
        return normalized switch
        {
            "" => PromptPlatforms.Generic,
            "generic" => PromptPlatforms.Generic,
            "codex" => PromptPlatforms.CodexCli,
            "codexcli" => PromptPlatforms.CodexCli,
            "claude" => PromptPlatforms.ClaudeCode,
            "claudecode" => PromptPlatforms.ClaudeCode,
            "opencode" => PromptPlatforms.OpenCode,
            "open-code" => PromptPlatforms.OpenCode,
            "gemini" => PromptPlatforms.GeminiCli,
            "geminicli" => PromptPlatforms.GeminiCli,
            "copilot" => PromptPlatforms.Copilot,
            "github-copilot" => PromptPlatforms.Copilot,
            "github-copilot-cli" => PromptPlatforms.Copilot,
            "copilotcli" => PromptPlatforms.Copilot,
            "antigravity" => PromptPlatforms.Antigravity,
            "google-antigravity" => PromptPlatforms.Antigravity,
            _ => throw new ArgumentException($"Unsupported prompt platform '{platform}'. Supported values: generic, codexcli, claudecode, opencode, geminicli, copilot, antigravity.")
        };
    }

    public static RenderedPrompt Render(string platform, PromptBundle bundle)
    {
        var normalizedPlatform = NormalizePlatform(platform);
        var messages = BuildMessages(normalizedPlatform, bundle);
        return new RenderedPrompt
        {
            Platform = normalizedPlatform,
            Messages = messages,
            CompositeText = BuildCompositeText(normalizedPlatform, messages)
        };
    }

    private static List<PromptMessage> BuildMessages(string platform, PromptBundle bundle)
    {
        var system = platform switch
        {
            PromptPlatforms.CodexCli => CombineSections(
                "SYSTEM",
                bundle.SystemLayer,
                "TOOLS",
                bundle.ToolLayer,
                "SKILLS",
                bundle.SkillLayer),
            PromptPlatforms.ClaudeCode => CombineSections(
                "ROLE",
                bundle.SystemLayer,
                "TOOL USE",
                bundle.ToolLayer,
                "PROJECT SKILLS",
                bundle.SkillLayer),
            PromptPlatforms.OpenCode => CombineSections(
                "PERSONA",
                bundle.SystemLayer,
                "TOOL POLICY",
                bundle.ToolLayer,
                "LOADED SKILLS",
                bundle.SkillLayer),
            PromptPlatforms.GeminiCli => CombineSections(
                "SYSTEM INSTRUCTIONS",
                bundle.SystemLayer,
                "TOOL CONTRACT",
                bundle.ToolLayer,
                "REPOSITORY SKILLS",
                bundle.SkillLayer),
            PromptPlatforms.Copilot => CombineSections(
                "INSTRUCTIONS",
                bundle.SystemLayer,
                "OPERATING CONSTRAINTS",
                bundle.ToolLayer,
                "CONTEXT SKILLS",
                bundle.SkillLayer),
            PromptPlatforms.Antigravity => CombineSections(
                "MISSION",
                bundle.SystemLayer,
                "EXECUTION TOOLS",
                bundle.ToolLayer,
                "HARNESS SKILLS",
                bundle.SkillLayer),
            _ => CombineSections(
                "SYSTEM",
                bundle.SystemLayer,
                "TOOLS",
                bundle.ToolLayer,
                "SKILLS",
                bundle.SkillLayer)
        };

        var user = platform switch
        {
            PromptPlatforms.ClaudeCode => CombineSections("TASK", bundle.RequestLayer),
            PromptPlatforms.OpenCode => CombineSections("TASK BRIEF", bundle.RequestLayer),
            PromptPlatforms.GeminiCli => CombineSections("USER REQUEST", bundle.RequestLayer),
            PromptPlatforms.Copilot => CombineSections("USER TASK", bundle.RequestLayer),
            PromptPlatforms.Antigravity => CombineSections("WORKFLOW", bundle.RequestLayer),
            _ => CombineSections("REQUEST", bundle.RequestLayer)
        };

        return new List<PromptMessage>
        {
            new()
            {
                Role = "system",
                Content = system
            },
            new()
            {
                Role = "user",
                Content = user
            }
        };
    }

    private static string BuildCompositeText(string platform, IReadOnlyList<PromptMessage> messages)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Platform: {platform}");
        builder.AppendLine();

        foreach (var message in messages)
        {
            builder.AppendLine($"{message.Role.ToUpperInvariant()} MESSAGE");
            builder.AppendLine(message.Content);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string CombineSections(params string[] parts)
    {
        if (parts.Length % 2 != 0)
        {
            throw new ArgumentException("Prompt sections must be provided as heading/content pairs.");
        }

        var builder = new StringBuilder();
        for (var index = 0; index < parts.Length; index += 2)
        {
            var heading = parts[index];
            var content = parts[index + 1];
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine(heading);
            builder.AppendLine(content.Trim());
        }

        return builder.ToString().TrimEnd();
    }
}
