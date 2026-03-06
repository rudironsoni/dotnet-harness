using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using YamlDotNet.Serialization;

namespace DotNetAgentHarness.Tools.Engine;

public static class SkillTestEngine
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    public static SkillTestSuiteResult Run(string repoRoot, string skillName, bool failFast, string? filter = null)
    {
        var skillsRoot = Path.Combine(repoRoot, ".rulesync", "skills");
        if (!Directory.Exists(skillsRoot))
        {
            throw new DirectoryNotFoundException($"Skills directory not found: {skillsRoot}");
        }

        var targets = ResolveTargets(skillsRoot, skillName);
        var results = new List<SkillTestResult>();

        foreach (var skillDirectory in targets)
        {
            var result = RunSkill(repoRoot, skillDirectory, filter);
            results.Add(result);

            if (failFast && !result.Passed)
            {
                break;
            }
        }

        var totalChecks = results.Sum(result => result.Checks.Count);
        var failedChecks = results.Sum(result => result.Checks.Count(check => !check.Passed));
        var totalCases = results.Sum(result => result.CaseCount);
        var skillsWithCases = results.Count(result => result.CaseCount > 0);

        return new SkillTestSuiteResult
        {
            Skills = results,
            Passed = failedChecks == 0,
            TotalCases = totalCases,
            SkillsWithCases = skillsWithCases,
            SkillsWithoutCases = results.Count - skillsWithCases,
            TotalChecks = totalChecks,
            FailedChecks = failedChecks
        };
    }

    public static string ToJunitXml(SkillTestSuiteResult suite)
    {
        var suiteElement = new XElement("testsuite",
            new XAttribute("name", "dotnet-agent-harness"),
            new XAttribute("tests", suite.TotalChecks),
            new XAttribute("failures", suite.FailedChecks));

        foreach (var skill in suite.Skills)
        {
            foreach (var check in skill.Checks)
            {
                var className = string.IsNullOrWhiteSpace(check.CaseName)
                    ? skill.SkillId
                    : $"{skill.SkillId}.{Sanitize(check.CaseName)}";
                var caseElement = new XElement("testcase",
                    new XAttribute("classname", className),
                    new XAttribute("name", check.Name));

                if (!check.Passed)
                {
                    caseElement.Add(new XElement("failure", check.Message));
                }

                suiteElement.Add(caseElement);
            }
        }

        var document = new XDocument(new XElement("testsuites", suiteElement));
        return document.ToString();
    }

    private static List<string> ResolveTargets(string skillsRoot, string skillName)
    {
        if (skillName.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetDirectories(skillsRoot)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var exactPath = Path.Combine(skillsRoot, skillName);
        if (!Directory.Exists(exactPath))
        {
            throw new InvalidOperationException($"Skill '{skillName}' not found under {skillsRoot}.");
        }

        return new List<string> { exactPath };
    }

    private static SkillTestResult RunSkill(string repoRoot, string skillDirectory, string? filter)
    {
        var skillId = Path.GetFileName(skillDirectory) ?? skillDirectory;
        var skillFile = Path.Combine(skillDirectory, "SKILL.md");
        var checks = new List<SkillTestCheck>();
        var caseCount = 0;

        if (!File.Exists(skillFile))
        {
            checks.Add(new SkillTestCheck
            {
                Name = "skill-file",
                Passed = false,
                Message = "Missing SKILL.md."
            });

            return new SkillTestResult
            {
                SkillId = skillId,
                SkillPath = Path.GetRelativePath(repoRoot, skillDirectory),
                Checks = checks,
                Passed = false
            };
        }

        var content = File.ReadAllText(skillFile);
        Dictionary<string, object> frontmatter;
        try
        {
            frontmatter = MarkdownFrontmatter.Parse(content);
            var hasRequiredFields = frontmatter.ContainsKey("name") && frontmatter.ContainsKey("description");
            checks.Add(new SkillTestCheck
            {
                Name = "frontmatter",
                Passed = hasRequiredFields,
                Message = hasRequiredFields
                    ? "Frontmatter parsed successfully."
                    : "Frontmatter is missing required fields.",
                SourceFile = Path.GetRelativePath(repoRoot, skillFile)
            });

            var declaredName = MarkdownFrontmatter.GetString(frontmatter, "name") ?? string.Empty;
            checks.Add(new SkillTestCheck
            {
                Name = "frontmatter-name",
                Passed = declaredName.Equals(skillId, StringComparison.OrdinalIgnoreCase),
                Message = declaredName.Equals(skillId, StringComparison.OrdinalIgnoreCase)
                    ? "Frontmatter name matches the skill directory."
                    : $"Frontmatter name '{declaredName}' does not match skill directory '{skillId}'.",
                SourceFile = Path.GetRelativePath(repoRoot, skillFile)
            });
        }
        catch (Exception ex)
        {
            frontmatter = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            checks.Add(new SkillTestCheck
            {
                Name = "frontmatter",
                Passed = false,
                Message = ex.Message,
                SourceFile = Path.GetRelativePath(repoRoot, skillFile)
            });
        }

        var testCasesDir = Path.Combine(skillDirectory, "test-cases");
        if (!Directory.Exists(testCasesDir))
        {
            checks.Add(new SkillTestCheck
            {
                Name = "test-cases",
                Passed = true,
                Message = "No test-cases directory present.",
                SourceFile = Path.GetRelativePath(repoRoot, skillFile)
            });
        }
        else
        {
            foreach (var filePath in EnumerateTestCaseFiles(testCasesDir))
            {
                SkillTestCaseDefinition definition;
                try
                {
                    definition = LoadDefinition(filePath);
                }
                catch (Exception ex)
                {
                    checks.Add(new SkillTestCheck
                    {
                        Name = $"{Path.GetFileName(filePath)}: parse",
                        Passed = false,
                        Message = ex.Message,
                        SourceFile = Path.GetRelativePath(repoRoot, filePath),
                        CaseName = Path.GetFileNameWithoutExtension(filePath)
                    });
                    continue;
                }

                var selectedTests = SelectTests(definition, filePath, filter);
                if (selectedTests.Count == 0)
                {
                    continue;
                }

                caseCount++;
                checks.AddRange(RunTestCase(repoRoot, skillDirectory, filePath, definition, selectedTests, content, frontmatter));
            }

            if (caseCount == 0)
            {
                checks.Add(new SkillTestCheck
                {
                    Name = "test-case-filter",
                    Passed = true,
                    Message = string.IsNullOrWhiteSpace(filter)
                        ? "No runnable test cases were found."
                        : $"No test cases matched filter '{filter}'.",
                    SourceFile = Path.GetRelativePath(repoRoot, testCasesDir)
                });
            }
        }

        return new SkillTestResult
        {
            SkillId = skillId,
            SkillPath = Path.GetRelativePath(repoRoot, skillDirectory),
            CaseCount = caseCount,
            Checks = checks,
            Passed = checks.All(check => check.Passed)
        };
    }

    private static IEnumerable<string> EnumerateTestCaseFiles(string testCasesDir)
    {
        return Directory.EnumerateFiles(testCasesDir, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static List<SkillTestDefinition> SelectTests(SkillTestCaseDefinition definition, string filePath, string? filter)
    {
        if (definition.Tests.Count == 0)
        {
            return new List<SkillTestDefinition>();
        }

        if (string.IsNullOrWhiteSpace(filter))
        {
            return definition.Tests;
        }

        var normalizedFilter = filter.Trim();
        if (definition.Name.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(filePath).Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase))
        {
            return definition.Tests;
        }

        return definition.Tests
            .Where(test => test.Name.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase)
                        || test.Description.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IEnumerable<SkillTestCheck> RunTestCase(
        string repoRoot,
        string skillDirectory,
        string filePath,
        SkillTestCaseDefinition definition,
        IReadOnlyList<SkillTestDefinition> tests,
        string skillContent,
        IReadOnlyDictionary<string, object> frontmatter)
    {
        var checks = new List<SkillTestCheck>();
        var workspace = Path.Combine(
            Path.GetTempPath(),
            "dotnet-agent-harness-skill-tests",
            Sanitize(Path.GetFileName(skillDirectory) ?? "skill"),
            $"{Sanitize(Path.GetFileNameWithoutExtension(filePath))}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);

        var caseRelativePath = Path.GetRelativePath(repoRoot, filePath);
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["repo_root"] = repoRoot,
            ["skill_dir"] = skillDirectory,
            ["skill_file"] = Path.Combine(skillDirectory, "SKILL.md"),
            ["workspace"] = workspace
        };

        try
        {
            if (!ExecuteCommands(
                    repoRoot,
                    caseRelativePath,
                    definition.Name,
                    "setup",
                    definition.SetupCommands,
                    tokens,
                    definition.WorkingDirectory,
                    checks))
            {
                return checks;
            }

            if (tests.Count == 0)
            {
                checks.Add(new SkillTestCheck
                {
                    Name = $"{definition.Name}: assertions",
                    Passed = false,
                    Message = "Test case loaded but contains no runnable tests.",
                    SourceFile = caseRelativePath,
                    CaseName = definition.Name
                });
                return checks;
            }

            foreach (var test in tests)
            {
                checks.AddRange(RunScenarioTest(caseRelativePath, definition, test, tokens, skillContent, frontmatter));
            }

            return checks;
        }
        finally
        {
            var teardownChecks = ExecuteCommands(
                repoRoot,
                caseRelativePath,
                definition.Name,
                "teardown",
                definition.TeardownCommands,
                tokens,
                definition.WorkingDirectory,
                checks,
                allowContinuationOnFailure: true);
            _ = teardownChecks;

            try
            {
                if (Directory.Exists(workspace))
                {
                    Directory.Delete(workspace, recursive: true);
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

    private static IEnumerable<SkillTestCheck> RunScenarioTest(
        string caseRelativePath,
        SkillTestCaseDefinition definition,
        SkillTestDefinition test,
        IReadOnlyDictionary<string, string> tokens,
        string skillContent,
        IReadOnlyDictionary<string, object> frontmatter)
    {
        var execution = ExecuteScenario(test, definition.WorkingDirectory, tokens, skillContent);
        var checks = new List<SkillTestCheck>();
        var prefix = string.IsNullOrWhiteSpace(test.Name) ? definition.Name : $"{definition.Name}: {test.Name}";

        var expected = test.Expected ?? new SkillTestExpectation();
        var hasAssertions = false;

        if (!string.IsNullOrWhiteSpace(expected.Status))
        {
            hasAssertions = true;
            var expectSuccess = expected.Status.Equals("success", StringComparison.OrdinalIgnoreCase);
            var passed = execution.TimedOut
                ? false
                : expectSuccess
                    ? execution.ExitCode == 0
                    : execution.ExitCode != 0;
            checks.Add(BuildCheck(
                prefix,
                "status",
                passed,
                passed
                    ? $"Command completed with expected status '{expected.Status}'."
                    : $"Expected status '{expected.Status}' but exit code was {execution.ExitCode}.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        foreach (var fragment in expected.OutputContains)
        {
            hasAssertions = true;
            var passed = execution.StandardOutput.Contains(fragment, StringComparison.OrdinalIgnoreCase);
            checks.Add(BuildCheck(
                prefix,
                $"output_contains '{fragment}'",
                passed,
                passed
                    ? $"Found '{fragment}' in stdout."
                    : $"Expected '{fragment}' in stdout.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        foreach (var pattern in expected.OutputMatches)
        {
            hasAssertions = true;
            var passed = Regex.IsMatch(execution.StandardOutput, pattern, RegexOptions.Multiline);
            checks.Add(BuildCheck(
                prefix,
                $"output_matches '{pattern}'",
                passed,
                passed
                    ? $"Stdout matched regex '{pattern}'."
                    : $"Stdout did not match regex '{pattern}'.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        foreach (var fragment in expected.ErrorContains)
        {
            hasAssertions = true;
            var passed = execution.StandardError.Contains(fragment, StringComparison.OrdinalIgnoreCase);
            checks.Add(BuildCheck(
                prefix,
                $"error_contains '{fragment}'",
                passed,
                passed
                    ? $"Found '{fragment}' in stderr."
                    : $"Expected '{fragment}' in stderr.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        if (expected.NoErrors)
        {
            hasAssertions = true;
            var passed = string.IsNullOrWhiteSpace(execution.StandardError);
            checks.Add(BuildCheck(
                prefix,
                "no_errors",
                passed,
                passed ? "stderr was empty." : "stderr contained output.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        foreach (var relativePath in expected.FileExists)
        {
            hasAssertions = true;
            var fullPath = ResolvePath(relativePath, definition.WorkingDirectory, test.WorkingDirectory, tokens);
            var passed = File.Exists(fullPath) || Directory.Exists(fullPath);
            checks.Add(BuildCheck(
                prefix,
                $"file_exists '{relativePath}'",
                passed,
                passed
                    ? $"Found '{relativePath}'."
                    : $"Expected '{relativePath}' to exist.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        foreach (var relativePath in expected.FileNotExists)
        {
            hasAssertions = true;
            var fullPath = ResolvePath(relativePath, definition.WorkingDirectory, test.WorkingDirectory, tokens);
            var passed = !File.Exists(fullPath) && !Directory.Exists(fullPath);
            checks.Add(BuildCheck(
                prefix,
                $"file_not_exists '{relativePath}'",
                passed,
                passed
                    ? $"Confirmed '{relativePath}' is absent."
                    : $"Expected '{relativePath}' to be absent.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        foreach (var fragment in expected.SkillContains)
        {
            hasAssertions = true;
            var passed = skillContent.Contains(fragment, StringComparison.OrdinalIgnoreCase);
            checks.Add(BuildCheck(
                prefix,
                $"skill_contains '{fragment}'",
                passed,
                passed
                    ? $"Found '{fragment}' in SKILL.md."
                    : $"Expected '{fragment}' in SKILL.md.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        foreach (var pattern in expected.SkillMatches)
        {
            hasAssertions = true;
            var passed = Regex.IsMatch(skillContent, pattern, RegexOptions.Multiline);
            checks.Add(BuildCheck(
                prefix,
                $"skill_matches '{pattern}'",
                passed,
                passed
                    ? $"SKILL.md matched regex '{pattern}'."
                    : $"SKILL.md did not match regex '{pattern}'.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        foreach (var pair in expected.Frontmatter)
        {
            hasAssertions = true;
            var actual = frontmatter.TryGetValue(pair.Key, out var value) ? ConvertToString(value) : string.Empty;
            var passed = actual.Equals(pair.Value, StringComparison.OrdinalIgnoreCase);
            checks.Add(BuildCheck(
                prefix,
                $"frontmatter.{pair.Key}",
                passed,
                passed
                    ? $"Frontmatter '{pair.Key}' matched '{pair.Value}'."
                    : $"Expected frontmatter '{pair.Key}' to be '{pair.Value}', got '{actual}'.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        if (!hasAssertions)
        {
            checks.Add(BuildCheck(
                prefix,
                "assertions",
                false,
                "Test case contains no assertions.",
                caseRelativePath,
                definition.Name,
                test.Name));
        }

        return checks;
    }

    private static ProcessExecutionResult ExecuteScenario(
        SkillTestDefinition test,
        string caseWorkingDirectory,
        IReadOnlyDictionary<string, string> tokens,
        string skillContent)
    {
        if (string.IsNullOrWhiteSpace(test.Command))
        {
            return new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = skillContent,
                StandardError = string.Empty
            };
        }

        var workingDirectory = ResolveWorkingDirectory(caseWorkingDirectory, test.WorkingDirectory, tokens);
        Directory.CreateDirectory(workingDirectory);
        var command = ReplaceTokens(test.Command, tokens);
        return ProcessRunner.RunShell(command, workingDirectory, test.TimeoutMs);
    }

    private static bool ExecuteCommands(
        string repoRoot,
        string caseRelativePath,
        string caseName,
        string phase,
        IReadOnlyList<SkillTestCommandDefinition> commands,
        IReadOnlyDictionary<string, string> tokens,
        string caseWorkingDirectory,
        List<SkillTestCheck> checks,
        bool allowContinuationOnFailure = false)
    {
        foreach (var commandDefinition in commands)
        {
            var workingDirectory = ResolveWorkingDirectory(caseWorkingDirectory, commandDefinition.WorkingDirectory, tokens);
            Directory.CreateDirectory(workingDirectory);
            var command = ReplaceTokens(commandDefinition.Command, tokens);
            var result = ProcessRunner.RunShell(command, workingDirectory, commandDefinition.TimeoutMs);
            var passed = !result.TimedOut && result.ExitCode == 0;
            checks.Add(new SkillTestCheck
            {
                Name = $"{caseName}: {phase} '{commandDefinition.Command}'",
                Passed = passed,
                Message = passed
                    ? $"Command succeeded in {workingDirectory}."
                    : result.TimedOut
                        ? $"Command timed out after {commandDefinition.TimeoutMs}ms."
                        : $"Command failed with exit code {result.ExitCode}. {FirstNonEmpty(result.StandardError, result.StandardOutput)}",
                SourceFile = caseRelativePath,
                CaseName = caseName
            });

            if (!passed && !allowContinuationOnFailure && !commandDefinition.ContinueOnError)
            {
                return false;
            }
        }

        return true;
    }

    private static SkillTestCheck BuildCheck(string prefix, string suffix, bool passed, string message, string sourceFile, string caseName, string testName)
    {
        return new SkillTestCheck
        {
            Name = $"{prefix}: {suffix}",
            Passed = passed,
            Message = message,
            SourceFile = sourceFile,
            CaseName = caseName,
            TestName = testName
        };
    }

    private static SkillTestCaseDefinition LoadDefinition(string filePath)
    {
        var root = ToDictionary(Deserializer.Deserialize<object>(File.ReadAllText(filePath)));
        var definition = new SkillTestCaseDefinition
        {
            Name = GetString(root, "name") ?? Path.GetFileNameWithoutExtension(filePath),
            Description = GetString(root, "description") ?? string.Empty,
            WorkingDirectory = GetString(root, "working_directory") ?? string.Empty,
            SetupCommands = ParseCommands(GetValue(root, "setup")),
            TeardownCommands = ParseCommands(GetValue(root, "teardown"))
        };

        var testsNode = GetValue(root, "tests");
        definition.Tests = testsNode is null
            ? ParseLegacyTests(root, definition)
            : ParseTests(testsNode, definition);

        return definition;
    }

    private static List<SkillTestDefinition> ParseTests(object node, SkillTestCaseDefinition definition)
    {
        var tests = new List<SkillTestDefinition>();
        var index = 0;
        foreach (var item in ToList(node))
        {
            index++;
            var map = ToDictionary(item);
            var test = new SkillTestDefinition
            {
                Name = GetString(map, "name") ?? $"test-{index}",
                Description = GetString(map, "description") ?? string.Empty,
                Input = GetInput(GetValue(map, "input")),
                Subject = GetString(map, "subject") ?? string.Empty,
                Command = GetString(map, "command") ?? GetString(map, "run") ?? string.Empty,
                WorkingDirectory = GetString(map, "working_directory") ?? string.Empty,
                TimeoutMs = GetInt(map, "timeout_ms", 60_000),
                Expected = MergeExpectations(
                    ParseExpectation(GetValue(map, "expected")),
                    ParseExpectation(item))
            };
            tests.Add(test);
        }

        return tests;
    }

    private static List<SkillTestDefinition> ParseLegacyTests(Dictionary<string, object?> root, SkillTestCaseDefinition definition)
    {
        var expectation = ParseExpectation(root);
        expectation.SkillContains.AddRange(GetStrings(GetValue(root, "expected_output_contains")));

        foreach (var validation in ToList(GetValue(root, "validation")))
        {
            var validationMap = ToDictionary(validation);
            var pattern = GetString(validationMap, "pattern");
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                expectation.SkillMatches.Add(pattern);
            }
        }

        if (!HasAssertions(expectation))
        {
            return new List<SkillTestDefinition>();
        }

        return new List<SkillTestDefinition>
        {
            new()
            {
                Name = definition.Name,
                Description = definition.Description,
                Input = GetInput(GetValue(root, "input")),
                Expected = expectation
            }
        };
    }

    private static SkillTestExpectation ParseExpectation(object? node)
    {
        var map = ToDictionary(node);
        var expectation = new SkillTestExpectation
        {
            Status = GetString(map, "status") ?? string.Empty,
            NoErrors = GetBool(map, "no_errors")
        };

        expectation.OutputContains.AddRange(GetStrings(GetValue(map, "output_contains")));
        expectation.OutputMatches.AddRange(GetStrings(GetValue(map, "output_matches")));
        expectation.ErrorContains.AddRange(GetStrings(GetValue(map, "error_contains")));
        expectation.FileExists.AddRange(GetStrings(GetValue(map, "file_exists")));
        expectation.FileNotExists.AddRange(GetStrings(GetValue(map, "file_not_exists")));
        expectation.SkillContains.AddRange(GetStrings(GetValue(map, "skill_contains")));
        expectation.SkillMatches.AddRange(GetStrings(GetValue(map, "skill_matches")));

        foreach (var pair in ToDictionary(GetValue(map, "frontmatter")))
        {
            var value = ConvertToString(pair.Value);
            if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(value))
            {
                expectation.Frontmatter[pair.Key] = value;
            }
        }

        return expectation;
    }

    private static SkillTestExpectation MergeExpectations(SkillTestExpectation primary, SkillTestExpectation secondary)
    {
        if (string.IsNullOrWhiteSpace(primary.Status))
        {
            primary.Status = secondary.Status;
        }

        primary.OutputContains.AddRange(secondary.OutputContains.Where(value => !primary.OutputContains.Contains(value, StringComparer.OrdinalIgnoreCase)));
        primary.OutputMatches.AddRange(secondary.OutputMatches.Where(value => !primary.OutputMatches.Contains(value, StringComparer.OrdinalIgnoreCase)));
        primary.ErrorContains.AddRange(secondary.ErrorContains.Where(value => !primary.ErrorContains.Contains(value, StringComparer.OrdinalIgnoreCase)));
        primary.FileExists.AddRange(secondary.FileExists.Where(value => !primary.FileExists.Contains(value, StringComparer.OrdinalIgnoreCase)));
        primary.FileNotExists.AddRange(secondary.FileNotExists.Where(value => !primary.FileNotExists.Contains(value, StringComparer.OrdinalIgnoreCase)));
        primary.SkillContains.AddRange(secondary.SkillContains.Where(value => !primary.SkillContains.Contains(value, StringComparer.OrdinalIgnoreCase)));
        primary.SkillMatches.AddRange(secondary.SkillMatches.Where(value => !primary.SkillMatches.Contains(value, StringComparer.OrdinalIgnoreCase)));
        primary.NoErrors = primary.NoErrors || secondary.NoErrors;

        foreach (var pair in secondary.Frontmatter)
        {
            primary.Frontmatter[pair.Key] = pair.Value;
        }

        return primary;
    }

    private static List<SkillTestCommandDefinition> ParseCommands(object? node)
    {
        var commands = new List<SkillTestCommandDefinition>();
        foreach (var item in ToList(node))
        {
            if (item is string commandText && !string.IsNullOrWhiteSpace(commandText))
            {
                commands.Add(new SkillTestCommandDefinition { Command = commandText });
                continue;
            }

            var map = ToDictionary(item);
            var command = GetString(map, "command") ?? GetString(map, "run");
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            commands.Add(new SkillTestCommandDefinition
            {
                Command = command,
                WorkingDirectory = GetString(map, "working_directory") ?? string.Empty,
                TimeoutMs = GetInt(map, "timeout_ms", 60_000),
                ContinueOnError = GetBool(map, "continue_on_error")
            });
        }

        return commands;
    }

    private static Dictionary<string, object?> ToDictionary(object? value)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (value is null)
        {
            return result;
        }

        if (value is IDictionary<string, object?> typed)
        {
            foreach (var pair in typed)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    result[key] = entry.Value;
                }
            }
        }

        return result;
    }

    private static List<object?> ToList(object? value)
    {
        if (value is null)
        {
            return new List<object?>();
        }

        if (value is string)
        {
            return new List<object?> { value };
        }

        if (value is IEnumerable enumerable)
        {
            var items = new List<object?>();
            foreach (var item in enumerable)
            {
                items.Add(item);
            }

            return items;
        }

        return new List<object?> { value };
    }

    private static object? GetValue(IReadOnlyDictionary<string, object?> map, string key)
    {
        return map.TryGetValue(key, out var value) ? value : null;
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> map, string key)
    {
        return ConvertToString(GetValue(map, key));
    }

    private static string ConvertToString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            IEnumerable sequence when value is not string => string.Join(", ", sequence.Cast<object?>().Select(ConvertToString).Where(text => !string.IsNullOrWhiteSpace(text))),
            _ => Convert.ToString(value) ?? string.Empty
        };
    }

    private static string GetInput(object? value)
    {
        if (value is string text)
        {
            return text;
        }

        var map = ToDictionary(value);
        return GetString(map, "query") ?? string.Empty;
    }

    private static int GetInt(IReadOnlyDictionary<string, object?> map, string key, int fallback)
    {
        var raw = GetString(map, key);
        return int.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    private static bool GetBool(IReadOnlyDictionary<string, object?> map, string key)
    {
        var raw = GetString(map, key);
        return bool.TryParse(raw, out var parsed) && parsed;
    }

    private static List<string> GetStrings(object? value)
    {
        return ToList(value)
            .Select(ConvertToString)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private static bool HasAssertions(SkillTestExpectation expectation)
    {
        return !string.IsNullOrWhiteSpace(expectation.Status)
               || expectation.OutputContains.Count > 0
               || expectation.OutputMatches.Count > 0
               || expectation.ErrorContains.Count > 0
               || expectation.FileExists.Count > 0
               || expectation.FileNotExists.Count > 0
               || expectation.SkillContains.Count > 0
               || expectation.SkillMatches.Count > 0
               || expectation.Frontmatter.Count > 0
               || expectation.NoErrors;
    }

    private static string ResolveWorkingDirectory(string caseWorkingDirectory, string testWorkingDirectory, IReadOnlyDictionary<string, string> tokens)
    {
        var candidate = !string.IsNullOrWhiteSpace(testWorkingDirectory)
            ? testWorkingDirectory
            : caseWorkingDirectory;
        var expanded = ReplaceTokens(candidate, tokens);

        if (string.IsNullOrWhiteSpace(expanded))
        {
            return tokens["workspace"];
        }

        return Path.IsPathRooted(expanded)
            ? expanded
            : Path.GetFullPath(Path.Combine(tokens["workspace"], expanded));
    }

    private static string ResolvePath(string relativePath, string caseWorkingDirectory, string testWorkingDirectory, IReadOnlyDictionary<string, string> tokens)
    {
        var expanded = ReplaceTokens(relativePath, tokens);
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        var workingDirectory = ResolveWorkingDirectory(caseWorkingDirectory, testWorkingDirectory, tokens);
        return Path.GetFullPath(Path.Combine(workingDirectory, expanded));
    }

    private static string ReplaceTokens(string? value, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var result = value;
        foreach (var pair in tokens)
        {
            result = result.Replace($"{{{pair.Key}}}", pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string Sanitize(string value)
    {
        var chars = value.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray();
        return chars.Length == 0 ? "case" : new string(chars);
    }
}
