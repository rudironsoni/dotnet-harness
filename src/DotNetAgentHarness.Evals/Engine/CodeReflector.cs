using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace DotNetAgentHarness.Evals.Engine;

public class CodeReflector(IChatClient chatClient)
{
    public async Task<string> ReflectAndFixAsync(string spec, int maxIterations = 3, CancellationToken cancellationToken = default)
    {
        var codeResponse = await chatClient.GetResponseAsync($"Write C# code for: {spec}", cancellationToken: cancellationToken);
        var code = codeResponse.Text ?? string.Empty;

        var testsResponse = await chatClient.GetResponseAsync($"Generate xUnit tests for: {spec}\nCode: {code}", cancellationToken: cancellationToken);
        var tests = testsResponse.Text ?? string.Empty;

        for (int i = 0; i < maxIterations; i++)
        {
            var result = await RunTestsAsync(code, tests, cancellationToken);
            if (result.Success)
            {
                return code;
            }
            
            var fixResponse = await chatClient.GetResponseAsync($"Fix error: {result.Error}\nCode: {code}", cancellationToken: cancellationToken);
            code = fixResponse.Text ?? string.Empty;
        }

        return code;
    }

    // Stub for actual test execution
    private Task<TestResult> RunTestsAsync(string code, string tests, CancellationToken cancellationToken = default)
    {
        // In a real implementation, you would compile and run the tests dynamically
        return Task.FromResult(new TestResult { Success = false, Error = "Mock test failure" });
    }

    private class TestResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
