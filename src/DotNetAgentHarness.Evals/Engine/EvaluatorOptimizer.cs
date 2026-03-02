using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace DotNetAgentHarness.Evals.Engine;

public class EvaluatorOptimizer(IChatClient chatClient, double scoreThreshold = 0.8)
{
    public async Task<string> GenerateAsync(string task, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync($"Complete: {task}", cancellationToken: cancellationToken);
        return response.Text ?? string.Empty;
    }

    public async Task<EvaluationResult> EvaluateAsync(string output, string task, CancellationToken cancellationToken = default)
    {
        var prompt = $$"""
            Evaluate output for task: {{task}}
            
            Output:
            {{output}}
            
            Return JSON in this format: { "overall_score": 0.0, "dimensions": { "accuracy": 0.0, "clarity": 0.0 } }
            """;

        var response = await chatClient.GetResponseAsync(
            prompt,
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
            cancellationToken);

        var jsonText = response.Text ?? "{}";
        
        try
        {
            return JsonSerializer.Deserialize<EvaluationResult>(jsonText) ?? new EvaluationResult();
        }
        catch (JsonException)
        {
            return new EvaluationResult();
        }
    }

    public async Task<string> OptimizeAsync(string output, EvaluationResult feedback, CancellationToken cancellationToken = default)
    {
        var feedbackJson = JsonSerializer.Serialize(feedback);
        var prompt = $"Improve based on feedback: {feedbackJson}\nOutput: {output}";
        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
        return response.Text ?? string.Empty;
    }

    public async Task<string> RunAsync(string task, int maxIterations = 3, CancellationToken cancellationToken = default)
    {
        var output = await GenerateAsync(task, cancellationToken);
        
        for (int i = 0; i < maxIterations; i++)
        {
            var evaluation = await EvaluateAsync(output, task, cancellationToken);
            if (evaluation.OverallScore >= scoreThreshold)
            {
                break;
            }
            output = await OptimizeAsync(output, evaluation, cancellationToken);
        }
        
        return output;
    }
}

public class EvaluationResult
{
    [JsonPropertyName("overall_score")]
    public double OverallScore { get; set; }
    
    [JsonPropertyName("dimensions")]
    public Dictionary<string, double> Dimensions { get; set; } = new();
}
