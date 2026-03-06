using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DotNetAgentHarness.Tools.Engine;

public static class ScaffoldEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<ScaffoldTemplate> LoadTemplates(string repoRoot)
    {
        var templatesRoot = Path.Combine(repoRoot, ".rulesync", "templates");
        if (!Directory.Exists(templatesRoot))
        {
            throw new DirectoryNotFoundException($"Templates directory not found: {templatesRoot}");
        }

        var templates = new List<ScaffoldTemplate>();
        foreach (var directory in Directory.GetDirectories(templatesRoot).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var templateId = Path.GetFileName(directory) ?? directory;
            var descriptorPath = Path.Combine(directory, "template.json");
            if (File.Exists(descriptorPath))
            {
                var template = JsonSerializer.Deserialize<ScaffoldTemplate>(File.ReadAllText(descriptorPath), JsonOptions)
                               ?? new ScaffoldTemplate { Id = templateId, DisplayName = templateId, Description = string.Empty };
                templates.Add(template);
            }
            else
            {
                templates.Add(new ScaffoldTemplate
                {
                    Id = templateId,
                    DisplayName = templateId,
                    Description = "Template descriptor not defined."
                });
            }
        }

        return templates;
    }

    public static ScaffoldPlan Plan(string repoRoot, string templateId, string destination, string solutionName)
    {
        var templates = LoadTemplates(repoRoot);
        var template = templates.FirstOrDefault(item => item.Id.Equals(templateId, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException($"Template '{templateId}' was not found.");

        var root = Path.GetFullPath(destination);
        var appName = $"{solutionName}.App";
        var testName = $"{solutionName}.Tests";
        var steps = new List<ScaffoldStep>
        {
            new() { Kind = "directory", Description = $"Create destination directory {root}", Command = $"mkdir -p \"{root}\"" },
            new() { Kind = "dotnet", Description = "Create solution file", Command = $"dotnet new sln -n {solutionName}" },
            new() { Kind = "file", Description = "Write AGENTS.md bootstrap file", Command = "write AGENTS.md" },
            new() { Kind = "file", Description = "Write .editorconfig", Command = "write .editorconfig" },
            new() { Kind = "file", Description = "Write Directory.Build.props", Command = "write Directory.Build.props" },
            new() { Kind = "dotnet", Description = "Create local tool manifest", Command = "dotnet new tool-manifest" }
        };

        switch (template.Id)
        {
            case "console":
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create console app", Command = $"dotnet new console -n {appName} -o src/{appName}" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create xUnit test project", Command = $"dotnet new xunit -n {testName} -o tests/{testName}" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Add solution entries", Command = $"dotnet sln add src/{appName}/{appName}.csproj tests/{testName}/{testName}.csproj" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Link tests to app project", Command = $"dotnet add tests/{testName}/{testName}.csproj reference src/{appName}/{appName}.csproj" });
                break;
            case "classlib":
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create class library", Command = $"dotnet new classlib -n {solutionName} -o src/{solutionName}" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create xUnit test project", Command = $"dotnet new xunit -n {testName} -o tests/{testName}" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Add solution entries", Command = $"dotnet sln add src/{solutionName}/{solutionName}.csproj tests/{testName}/{testName}.csproj" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Link tests to library project", Command = $"dotnet add tests/{testName}/{testName}.csproj reference src/{solutionName}/{solutionName}.csproj" });
                break;
            case "web-api":
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create web API", Command = $"dotnet new webapi -n {appName} -o src/{appName}" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create xUnit test project", Command = $"dotnet new xunit -n {testName} -o tests/{testName}" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Add solution entries", Command = $"dotnet sln add src/{appName}/{appName}.csproj tests/{testName}/{testName}.csproj" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Link tests to API project", Command = $"dotnet add tests/{testName}/{testName}.csproj reference src/{appName}/{appName}.csproj" });
                break;
            case "blazor-app":
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create Blazor app", Command = $"dotnet new blazor -n {appName} -o src/{appName}" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create solution entry", Command = $"dotnet sln add src/{appName}/{appName}.csproj" });
                break;
            case "maui-mobile":
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create MAUI app", Command = $"dotnet new maui -n {appName} -o src/{appName}" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create solution entry", Command = $"dotnet sln add src/{appName}/{appName}.csproj" });
                break;
            case "clean-arch":
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create Domain project", Command = $"dotnet new classlib -n {solutionName}.Domain -o src/{solutionName}.Domain" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create Application project", Command = $"dotnet new classlib -n {solutionName}.Application -o src/{solutionName}.Application" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create Infrastructure project", Command = $"dotnet new classlib -n {solutionName}.Infrastructure -o src/{solutionName}.Infrastructure" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create Web API project", Command = $"dotnet new webapi -n {solutionName}.Web -o src/{solutionName}.Web" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Create xUnit test project", Command = $"dotnet new xunit -n {solutionName}.Application.Tests -o tests/{solutionName}.Application.Tests" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Add solution entries", Command = $"dotnet sln add src/{solutionName}.Domain/{solutionName}.Domain.csproj src/{solutionName}.Application/{solutionName}.Application.csproj src/{solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj src/{solutionName}.Web/{solutionName}.Web.csproj tests/{solutionName}.Application.Tests/{solutionName}.Application.Tests.csproj" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Reference Domain from Application", Command = $"dotnet add src/{solutionName}.Application/{solutionName}.Application.csproj reference src/{solutionName}.Domain/{solutionName}.Domain.csproj" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Reference Application and Domain from Infrastructure", Command = $"dotnet add src/{solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj reference src/{solutionName}.Application/{solutionName}.Application.csproj src/{solutionName}.Domain/{solutionName}.Domain.csproj" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Reference Application and Infrastructure from Web", Command = $"dotnet add src/{solutionName}.Web/{solutionName}.Web.csproj reference src/{solutionName}.Application/{solutionName}.Application.csproj src/{solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj" });
                steps.Add(new ScaffoldStep { Kind = "dotnet", Description = "Reference Application and Domain from tests", Command = $"dotnet add tests/{solutionName}.Application.Tests/{solutionName}.Application.Tests.csproj reference src/{solutionName}.Application/{solutionName}.Application.csproj src/{solutionName}.Domain/{solutionName}.Domain.csproj" });
                break;
            default:
                throw new InvalidOperationException($"Template '{template.Id}' is not supported by scaffold planning.");
        }

        return new ScaffoldPlan
        {
            TemplateId = template.Id,
            Destination = root,
            SolutionName = solutionName,
            Steps = steps
        };
    }

    public static ScaffoldPlan Execute(string repoRoot, string templateId, string destination, string solutionName, bool force)
    {
        var plan = Plan(repoRoot, templateId, destination, solutionName);
        if (Directory.Exists(plan.Destination) && Directory.EnumerateFileSystemEntries(plan.Destination).Any() && !force)
        {
            throw new InvalidOperationException($"Destination '{plan.Destination}' is not empty. Pass --force to scaffold into an existing directory.");
        }

        Directory.CreateDirectory(plan.Destination);
        ProcessRunner.Run("dotnet", $"new sln -n {plan.SolutionName}", plan.Destination);
        WriteBootstrapFiles(plan.Destination, templateId, solutionName);
        ProcessRunner.Run("dotnet", "new tool-manifest", plan.Destination);

        foreach (var step in plan.Steps.Where(step => step.Kind == "dotnet" && !step.Command.StartsWith("dotnet new sln", StringComparison.OrdinalIgnoreCase) && !step.Command.Equals("dotnet new tool-manifest", StringComparison.OrdinalIgnoreCase)))
        {
            ProcessRunner.Run("dotnet", step.Command["dotnet ".Length..], plan.Destination);
        }

        return plan;
    }

    private static void WriteBootstrapFiles(string destination, string templateId, string solutionName)
    {
        File.WriteAllText(Path.Combine(destination, "AGENTS.md"), $$"""
            # {{solutionName}}

            Agent bootstrap generated by `dotnet-agent-harness scaffold`.

            - Run `dotnet-agent-harness init` after opening the repo.
            - Start with `dotnet-agent-harness analyze` and `dotnet-agent-harness recommend`.
            - Use `dotnet-agent-harness doctor` before large changes.
            - Use `dotnet-agent-harness review` and `dotnet-agent-harness validate` before submission.

            Template: `{{templateId}}`
            """);

        File.WriteAllText(Path.Combine(destination, ".editorconfig"), """
            root = true

            [*.cs]
            indent_style = space
            indent_size = 4
            csharp_new_line_before_open_brace = all
            dotnet_style_qualification_for_field = false:suggestion
            """);

        File.WriteAllText(Path.Combine(destination, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);
    }
}
