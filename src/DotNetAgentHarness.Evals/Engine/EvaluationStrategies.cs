using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace DotNetAgentHarness.Evals.Engine;

public class EvaluationStrategies(IChatClient chatClient)
{
    public async Task<string> EvaluateOutcomeAsync(string task, string output, string expected, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync(
            $"Does output achieve expected outcome? Task: {task}, Expected: {expected}, Output: {output}",
            cancellationToken: cancellationToken
        );
        return response.Text ?? string.Empty;
    }

    public async Task<string> LlmJudgeAsync(string outputA, string outputB, string criteria, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync(
            $"Compare outputs A and B for {criteria}. Which is better and why?\n\nOutput A:\n{outputA}\n\nOutput B:\n{outputB}",
            cancellationToken: cancellationToken
        );
        return response.Text ?? string.Empty;
    }

    public async Task<double> EvaluateWithRubricAsync(string output, Dictionary<string, RubricDimension> rubric, CancellationToken cancellationToken = default)
    {
        var dimensions = string.Join(", ", rubric.Keys);
        var prompt = $"Rate 1-5 for each dimension: {dimensions}\nOutput: {output}\n\nReturn ONLY a JSON dictionary where keys are dimensions and values are numbers.";
        
        var response = await chatClient.GetResponseAsync(
            prompt,
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
            cancellationToken);

        var jsonText = response.Text ?? "{}";
        Dictionary<string, double> scores;
        
        try
        {
            scores = JsonSerializer.Deserialize<Dictionary<string, double>>(jsonText) ?? new Dictionary<string, double>();
        }
        catch (JsonException)
        {
            scores = new Dictionary<string, double>();
        }
        
        double totalScore = 0;
        foreach (var dimension in rubric.Keys)
        {
            if (scores.TryGetValue(dimension, out var score))
            {
                totalScore += score * rubric[dimension].Weight;
            }
        }
        
        return totalScore / 5.0; // Normalize
    }
}

public class RubricDimension
{
    public double Weight { get; set; }
}
