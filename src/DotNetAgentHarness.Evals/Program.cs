using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNetAgentHarness.Evals.Engine;
using DotNetAgentHarness.Evals.Models;
using Microsoft.Extensions.AI;

namespace DotNetAgentHarness.Evals;

public class Program
{
    private const string DummyModeEnvironmentVariable = "DOTNET_AGENT_HARNESS_EVAL_DUMMY_MODE";
    private const string ProviderEnvironmentVariable = "DOTNET_AGENT_HARNESS_EVAL_PROVIDER";
    private const string TrialsEnvironmentVariable = "DOTNET_AGENT_HARNESS_EVAL_TRIALS";
    private const string CasesEnvironmentVariable = "DOTNET_AGENT_HARNESS_EVAL_CASES";
    private const string OpenAiModelEnvironmentVariable = "EVAL_OPENAI_MODEL";
    private const string OpenAiKeyEnvironmentVariable = "EVAL_OPENAI_KEY";
    private const string OpenAiEndpointEnvironmentVariable = "EVAL_OPENAI_ENDPOINT";

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Starting .NET Agent Harness Evaluations...");

        try
        {
            var options = EvalRunOptions.Parse(args);
            Console.WriteLine($"LLM mode: {(options.UseDummyMode ? "dummy" : "real")} ({options.ModeSource})");
            Console.WriteLine($"Provider: {options.Provider} | Trials(default): {options.DefaultTrialCount} | Cases: {options.CaseFilePath}");

            if (options.UseDummyMode && IsRunningInCi())
            {
                Console.Error.WriteLine(
                    "CI/release validation cannot run in dummy eval mode. Set DOTNET_AGENT_HARNESS_EVAL_DUMMY_MODE=false or pass --real-mode.");
                return 2;
            }

            var evalCases = YamlParser.LoadCases(options.CaseFilePath);
            Console.WriteLine($"Loaded {evalCases.Count} evaluation cases.");

            using var chatClient = options.UseDummyMode ? null : ChatClientFactory.Create(options.ToChatClientFactoryOptions());

            var allCaseResults = new List<CaseAggregateResult>(evalCases.Count);

            foreach (var evalCase in evalCases)
            {
                var trialCount = evalCase.TrialCount ?? options.DefaultTrialCount;
                if (trialCount <= 0)
                {
                    throw new InvalidOperationException($"Case '{evalCase.Id}' resolved to an invalid trial count '{trialCount}'.");
                }

                Console.WriteLine($"\nRunning Case: {evalCase.Id} ({evalCase.Description})");
                Console.WriteLine($"Expected trigger: {DisplayTrigger(evalCase.ExpectedTrigger)} | Trials: {trialCount}");

                var trialResults = new List<TrialResult>(trialCount);

                for (var trialIndex = 1; trialIndex <= trialCount; trialIndex++)
                {
                    TrialResult trialResult;
                    if (options.UseDummyMode)
                    {
                        trialResult = RunDummyTrial(evalCase, trialIndex);
                    }
                    else
                    {
                        if (chatClient is null)
                        {
                            throw new InvalidOperationException("Chat client was not initialized for real-mode evaluation.");
                        }

                        trialResult = await RunRealTrialAsync(chatClient, evalCase, trialIndex, CancellationToken.None);
                    }

                    var assertionResults = evalCase.Assertions
                        .Select(assertion => AssertionRunner.Evaluate(trialResult.ResponseContent, assertion))
                        .ToList();

                    var triggerEvaluation = TriggerEvaluator.Evaluate(evalCase.ExpectedTrigger, trialResult.Trigger);
                    var triggerResult = new TriggerCheckResult(triggerEvaluation.Passed, triggerEvaluation.Message);
                    var passed = triggerResult.Passed && assertionResults.All(r => r.Passed);

                    trialResult = trialResult with
                    {
                        Passed = passed,
                        AssertionResults = assertionResults,
                        TriggerResult = triggerResult
                    };

                    trialResults.Add(trialResult);
                    WriteTrialResult(trialResult);
                }

                var aggregateResult = BuildCaseAggregate(evalCase, trialResults);
                allCaseResults.Add(aggregateResult);
                WriteCaseAggregate(aggregateResult);
            }

            WriteOverallSummary(allCaseResults);

            if (!string.IsNullOrWhiteSpace(options.ArtifactPath))
            {
                var artifact = BuildArtifact(options, evalCases, allCaseResults);
                var artifactPath = EvalArtifactWriter.Write(options.ArtifactPath, artifact);
                Console.WriteLine($"Artifact written to {artifactPath}");
            }

            return allCaseResults.SelectMany(result => result.Trials).All(trial => trial.Passed) ? 0 : 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Invalid eval configuration: {ex.Message}");
            return 2;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Eval runtime error: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 3;
        }
    }

    private static TrialResult RunDummyTrial(EvalCase evalCase, int trialNumber)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = string.IsNullOrWhiteSpace(evalCase.FixtureResponse)
            ? "Deterministic fixture response not provided for this case."
            : evalCase.FixtureResponse;

        var trigger = string.IsNullOrWhiteSpace(evalCase.FixtureTrigger)
            ? evalCase.ExpectedTrigger
            : evalCase.FixtureTrigger;

        stopwatch.Stop();

        return new TrialResult(
            TrialNumber: trialNumber,
            ResponseContent: response,
            Trigger: trigger,
            ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
            TokenProxy: "fixture",
            Passed: false,
            AssertionResults: [],
            TriggerResult: TriggerCheckResult.Placeholder);
    }

    private static async Task<TrialResult> RunRealTrialAsync(
        IChatClient chatClient,
        EvalCase evalCase,
        int trialNumber,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var prompt = BuildEvalPrompt(evalCase.Prompt);

        var response = await chatClient.GetResponseAsync(
            prompt,
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
            cancellationToken);

        stopwatch.Stop();

        var parsedOutput = ParseModelOutput(response.Text ?? string.Empty);
        var tokenProxy = ResolveTokenProxy(response);

        return new TrialResult(
            TrialNumber: trialNumber,
            ResponseContent: parsedOutput.Response,
            Trigger: parsedOutput.Trigger,
            ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
            TokenProxy: tokenProxy,
            Passed: false,
            AssertionResults: [],
            TriggerResult: TriggerCheckResult.Placeholder);
    }

    private static string BuildEvalPrompt(string userPrompt)
    {
        return $$"""
            You are evaluating agent skill routing behavior.
            For the user prompt below, return ONLY JSON with this schema:
            { "response": "assistant answer", "trigger": "skill-id-or-none" }

            Rules:
            - trigger MUST be a single skill id (for example: dotnet-architect) or "none".
            - response MUST be plain text without markdown fences.
            - Do not add keys beyond response and trigger.

            User prompt:
            {{userPrompt}}
            """;
    }

    private static ModelOutput ParseModelOutput(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return new ModelOutput(string.Empty, string.Empty);
        }

        try
        {
            var output = JsonSerializer.Deserialize<ModelOutput>(
                responseText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (output is null)
            {
                return new ModelOutput(responseText, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(output.Response))
            {
                return new ModelOutput(responseText, output.Trigger ?? string.Empty);
            }

            return new ModelOutput(output.Response, output.Trigger ?? string.Empty);
        }
        catch (JsonException)
        {
            return new ModelOutput(responseText, string.Empty);
        }
    }

    private static string ResolveTokenProxy(ChatResponse response)
    {
        if (response.Usage is null)
        {
            return "n/a";
        }

        if (response.Usage.TotalTokenCount > 0)
        {
            return response.Usage.TotalTokenCount?.ToString() ?? "n/a";
        }

        if (response.Usage.InputTokenCount > 0 || response.Usage.OutputTokenCount > 0)
        {
            return $"in:{response.Usage.InputTokenCount},out:{response.Usage.OutputTokenCount}";
        }

        return "n/a";
    }

    private static CaseAggregateResult BuildCaseAggregate(EvalCase evalCase, IReadOnlyList<TrialResult> trials)
    {
        var passedCount = trials.Count(trial => trial.Passed);
        var failedCount = trials.Count - passedCount;
        var passRate = trials.Count == 0 ? 0 : (double)passedCount / trials.Count;
        var averageElapsed = trials.Count == 0 ? 0 : trials.Average(trial => trial.ElapsedMilliseconds);

        return new CaseAggregateResult(evalCase.Id, passedCount, failedCount, passRate, averageElapsed, trials);
    }

    private static void WriteTrialResult(TrialResult trialResult)
    {
        var status = trialResult.Passed ? "PASS" : "FAIL";
        Console.WriteLine(
            $"  Trial {trialResult.TrialNumber}: {status} | elapsed_ms={trialResult.ElapsedMilliseconds} | token_proxy={trialResult.TokenProxy}");

        if (!trialResult.TriggerResult.Passed)
        {
            Console.Error.WriteLine($"    Trigger mismatch: {trialResult.TriggerResult.Message}");
            Console.Error.WriteLine($"    Response preview: {Preview(trialResult.ResponseContent)}");
        }

        foreach (var assertion in trialResult.AssertionResults.Where(assertionResult => !assertionResult.Passed))
        {
            Console.Error.WriteLine($"    Assertion failed: {assertion.Message}");
        }
    }

    private static void WriteCaseAggregate(CaseAggregateResult aggregateResult)
    {
        Console.WriteLine(
            $"Case summary: pass={aggregateResult.PassedTrials}, fail={aggregateResult.FailedTrials}, pass_rate={aggregateResult.PassRate:P1}, avg_elapsed_ms={aggregateResult.AverageElapsedMilliseconds:F1}");
    }

    private static void WriteOverallSummary(IReadOnlyList<CaseAggregateResult> allCaseResults)
    {
        var totalTrials = allCaseResults.Sum(caseResult => caseResult.Trials.Count);
        var totalPassed = allCaseResults.Sum(caseResult => caseResult.PassedTrials);
        var totalFailed = totalTrials - totalPassed;
        var overallPassRate = totalTrials == 0 ? 0 : (double)totalPassed / totalTrials;

        Console.WriteLine("\n=== Eval Summary ===");
        Console.WriteLine($"Cases: {allCaseResults.Count}");
        Console.WriteLine($"Trials: {totalTrials}");
        Console.WriteLine($"Passed: {totalPassed}");
        Console.WriteLine($"Failed: {totalFailed}");
        Console.WriteLine($"Pass rate: {overallPassRate:P1}");
    }

    private static string Preview(string text)
    {
        const int maxLength = 160;
        if (string.IsNullOrEmpty(text))
        {
            return "<empty>";
        }

        var singleLine = text.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        if (singleLine.Length <= maxLength)
        {
            return singleLine;
        }

        return singleLine[..maxLength] + "...";
    }

    private static bool IsRunningInCi()
    {
        return string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveDefaultCasesPath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(CasesEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        var defaultPath = Path.Combine(AppContext.BaseDirectory, "../../../../../tests/eval/cases/routing.yaml");
        return Path.GetFullPath(defaultPath);
    }

    private static string DisplayTrigger(string value)
    {
        return TriggerEvaluator.Display(value);
    }

    private static EvalRunArtifact BuildArtifact(
        EvalRunOptions options,
        IReadOnlyList<EvalCase> evalCases,
        IReadOnlyList<CaseAggregateResult> allCaseResults)
    {
        var caseIndex = evalCases.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var cases = allCaseResults.Select(result =>
        {
            var evalCase = caseIndex[result.CaseId];
            var failures = result.Trials
                .Where(trial => !trial.Passed)
                .Select(trial =>
                {
                    var assertionMessages = trial.AssertionResults
                        .Where(assertion => !assertion.Passed)
                        .Select(assertion => assertion.Message)
                        .ToList();
                    var summary = !trial.TriggerResult.Passed
                        ? trial.TriggerResult.Message
                        : assertionMessages.FirstOrDefault() ?? "Trial failed without a detailed message.";

                    return new EvalArtifactFailure
                    {
                        TrialNumber = trial.TrialNumber,
                        TriggerMessage = trial.TriggerResult.Message,
                        AssertionMessages = assertionMessages,
                        Summary = summary
                    };
                })
                .ToList();

            return new EvalArtifactCase
            {
                CaseId = result.CaseId,
                Description = evalCase.Description,
                Prompt = evalCase.Prompt,
                ExpectedTrigger = evalCase.ExpectedTrigger,
                TrialCount = result.Trials.Count,
                PassedTrials = result.PassedTrials,
                FailedTrials = result.FailedTrials,
                PassRate = result.PassRate,
                AverageElapsedMilliseconds = result.AverageElapsedMilliseconds,
                Failures = failures
            };
        }).ToList();

        var totalTrials = allCaseResults.Sum(result => result.Trials.Count);
        var totalPassed = allCaseResults.Sum(result => result.PassedTrials);
        var totalFailed = totalTrials - totalPassed;

        return new EvalRunArtifact
        {
            RunId = options.RunId,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            UseDummyMode = options.UseDummyMode,
            ModeSource = options.ModeSource,
            Provider = options.Provider,
            Model = options.Model,
            CaseFilePath = options.CaseFilePath,
            DefaultTrialCount = options.DefaultTrialCount,
            Gate = options.Gate,
            PolicyProfile = options.PolicyProfile,
            PromptEvidenceId = options.PromptEvidenceId,
            Overall = new EvalArtifactOverall
            {
                CaseCount = allCaseResults.Count,
                TrialCount = totalTrials,
                PassedTrials = totalPassed,
                FailedTrials = totalFailed,
                PassRate = totalTrials == 0 ? 0 : (double)totalPassed / totalTrials
            },
            Cases = cases
        };
    }

    private sealed record EvalRunOptions(
        bool UseDummyMode,
        string ModeSource,
        string Provider,
        string Model,
        string? ApiKey,
        string? Endpoint,
        string CaseFilePath,
        int DefaultTrialCount,
        string? ArtifactPath,
        string Gate,
        string PolicyProfile,
        string? PromptEvidenceId,
        string RunId)
    {
        public ChatClientFactoryOptions ToChatClientFactoryOptions()
        {
            return new ChatClientFactoryOptions
            {
                Provider = Provider,
                Model = Model,
                ApiKey = ApiKey,
                Endpoint = Endpoint
            };
        }

        public static EvalRunOptions Parse(string[] args)
        {
            var useDummyMode = ResolveDummyMode(args, out var modeSource);

            var provider = ResolveValue(args, "--provider")
                ?? Environment.GetEnvironmentVariable(ProviderEnvironmentVariable)
                ?? "openai";

            var model = ResolveValue(args, "--model")
                ?? Environment.GetEnvironmentVariable(OpenAiModelEnvironmentVariable)
                ?? "gpt-4.1-mini";

            var apiKey = ResolveValue(args, "--api-key")
                ?? Environment.GetEnvironmentVariable(OpenAiKeyEnvironmentVariable)
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            var endpoint = ResolveValue(args, "--endpoint")
                ?? Environment.GetEnvironmentVariable(OpenAiEndpointEnvironmentVariable);

            var casesPath = ResolveValue(args, "--cases") ?? ResolveDefaultCasesPath();
            var defaultTrialCount = ResolveTrialCount(args);
            var gate = ResolveValue(args, "--gate") ?? string.Empty;
            var policyProfile = ResolveValue(args, "--policy-profile") ?? string.Empty;
            var promptEvidenceId = ResolveValue(args, "--prompt-evidence");
            var artifactPath = ResolveArtifactPath(args);
            var runId = ResolveValue(args, "--run-id") ?? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");

            return new EvalRunOptions(
                UseDummyMode: useDummyMode,
                ModeSource: modeSource,
                Provider: provider,
                Model: model,
                ApiKey: apiKey,
                Endpoint: endpoint,
                CaseFilePath: casesPath,
                DefaultTrialCount: defaultTrialCount,
                ArtifactPath: artifactPath,
                Gate: gate,
                PolicyProfile: policyProfile,
                PromptEvidenceId: promptEvidenceId,
                RunId: runId);
        }

        private static string? ResolveArtifactPath(string[] args)
        {
            var explicitPath = ResolveValue(args, "--artifact") ?? ResolveValue(args, "--artifact-path");
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                return Path.GetFullPath(explicitPath);
            }

            var artifactId = ResolveValue(args, "--artifact-id");
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                ".dotnet-agent-harness",
                "evidence",
                "evals",
                $"{artifactId}.json"));
        }

        private static int ResolveTrialCount(string[] args)
        {
            var fromArgs = ResolveValue(args, "--trials");
            if (!string.IsNullOrWhiteSpace(fromArgs))
            {
                return ParseTrialCount(fromArgs, "--trials");
            }

            var fromEnvironment = Environment.GetEnvironmentVariable(TrialsEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return ParseTrialCount(fromEnvironment, TrialsEnvironmentVariable);
            }

            return 1;
        }

        private static int ParseTrialCount(string rawValue, string sourceName)
        {
            if (!int.TryParse(rawValue, out var count) || count <= 0)
            {
                throw new ArgumentException($"{sourceName} must be an integer >= 1. Received '{rawValue}'.");
            }

            return count;
        }

        private static bool ResolveDummyMode(string[] args, out string modeSource)
        {
            if (TryParseDummyModeFromArgs(args, out var modeFromArgs))
            {
                modeSource = "command-line";
                return modeFromArgs;
            }

            var envValue = Environment.GetEnvironmentVariable(DummyModeEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                if (!bool.TryParse(envValue, out var modeFromEnv))
                {
                    throw new ArgumentException(
                        $"Environment variable {DummyModeEnvironmentVariable} must be 'true' or 'false'. Received '{envValue}'.");
                }

                modeSource = $"env:{DummyModeEnvironmentVariable}";
                return modeFromEnv;
            }

            modeSource = "default";
            return true;
        }

        private static bool TryParseDummyModeFromArgs(string[] args, out bool useDummyMode)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (string.Equals(arg, "--real-mode", StringComparison.OrdinalIgnoreCase))
                {
                    useDummyMode = false;
                    return true;
                }

                if (string.Equals(arg, "--dummy-mode", StringComparison.OrdinalIgnoreCase))
                {
                    var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                    if (!hasValue)
                    {
                        useDummyMode = true;
                        return true;
                    }

                    if (!bool.TryParse(args[i + 1], out useDummyMode))
                    {
                        throw new ArgumentException(
                            $"Invalid value '{args[i + 1]}' for --dummy-mode. Use true or false.");
                    }

                    return true;
                }

                if (arg.StartsWith("--dummy-mode=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = arg["--dummy-mode=".Length..];
                    if (!bool.TryParse(value, out useDummyMode))
                    {
                        throw new ArgumentException($"Invalid value '{value}' for --dummy-mode. Use true or false.");
                    }

                    return true;
                }
            }

            useDummyMode = true;
            return false;
        }

        private static string? ResolveValue(string[] args, string optionName)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException($"Missing value for {optionName}.");
                    }

                    return args[i + 1];
                }

                var withEquals = optionName + "=";
                if (args[i].StartsWith(withEquals, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i][withEquals.Length..];
                }
            }

            return null;
        }
    }

    private sealed record TrialResult(
        int TrialNumber,
        string ResponseContent,
        string Trigger,
        long ElapsedMilliseconds,
        string TokenProxy,
        bool Passed,
        IReadOnlyList<AssertionResult> AssertionResults,
        TriggerCheckResult TriggerResult);

    private sealed record CaseAggregateResult(
        string CaseId,
        int PassedTrials,
        int FailedTrials,
        double PassRate,
        double AverageElapsedMilliseconds,
        IReadOnlyList<TrialResult> Trials);

    private sealed record TriggerCheckResult(bool Passed, string Message)
    {
        public static TriggerCheckResult Placeholder { get; } = new(false, string.Empty);
    }

    private sealed record ModelOutput(string Response, string Trigger);
}
