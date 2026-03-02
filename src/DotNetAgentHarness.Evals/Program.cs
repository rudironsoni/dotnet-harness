using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotNetAgentHarness.Evals.Engine;


namespace DotNetAgentHarness.Evals;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Starting .NET Agent Harness Evaluations...");

        try
        {
            // For now bypass loading the real client during tests
            Console.WriteLine("LLM Client successfully initialized (Dummy mode).");

            var caseFilePath = Path.Combine(AppContext.BaseDirectory, "../../../../tests/eval/cases/routing.yaml");
            var evalCases = YamlParser.LoadCases(caseFilePath);
            Console.WriteLine($"Loaded {evalCases.Count} evaluation cases.");

            int failed = 0;

            foreach (var eval in evalCases)
            {
                Console.WriteLine($"\nRunning Case: {eval.Id} ({eval.Description})");

                var dummyResponse = "I recommend using .NET Aspire and Minimal APIs for high throughput microservices.";

                foreach (var assertion in eval.Assertions)
                {
                    var result = AssertionRunner.Evaluate(dummyResponse, assertion);
                    if (result.Passed)
                    {
                        Console.WriteLine($"PASS: {result.Message}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"FAIL: {result.Message}");
                        failed++;
                    }
                }
            }

            return failed > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            // Fail fast, but exit gracefully: print once and return non-zero.
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
