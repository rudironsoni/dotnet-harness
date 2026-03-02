using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace DotNetAgentHarness.Evals.Engine;

public class BasicReflection(IChatClient chatClient)
{
    public async Task<string> ReflectAndRefineAsync(string task, string[] criteria, int maxIterations = 3, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync($"Complete this task:\n{task}", cancellationToken: cancellationToken);
        var output = response.Text ?? string.Empty;

        for (int i = 0; i < maxIterations; i++)
        {
            var prompt = $"""
                Evaluate this output against criteria:
                {string.Join(", ", criteria)}
                
                Output:
                {output}
                
                Rate each criteria. Return ONLY a JSON object where keys are the criteria and values are objects with a 'status' ("PASS" or "FAIL") and 'feedback' string.
                """;

            var critiqueResponse = await chatClient.GetResponseAsync(
                prompt,
                new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
                cancellationToken);
            
            var critiqueText = critiqueResponse.Text ?? "{}";
            Dictionary<string, CritiqueResult>? critiqueData = null;

            try
            {
                critiqueData = JsonSerializer.Deserialize<Dictionary<string, CritiqueResult>>(critiqueText);
            }
            catch (JsonException)
            {
                // Fallback to empty if json parsing fails
                critiqueData = new Dictionary<string, CritiqueResult>();
            }
            
            if (critiqueData != null && critiqueData.Count > 0 && critiqueData.Values.All(c => c.Status == "PASS"))
            {
                return output;
            }

            var failed = critiqueData?
                .Where(kvp => kvp.Value.Status == "FAIL")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Feedback) ?? new Dictionary<string, string>();

            if (failed.Count == 0)
            {
                // If nothing explicitly failed but parsing succeeded, break out to avoid unguided infinite loops
                break;
            }

            var failedJson = JsonSerializer.Serialize(failed);

            var refinePrompt = $"Improve the original output to address these failures: {failedJson}\nOriginal Output: {output}";
            var improvedResponse = await chatClient.GetResponseAsync(refinePrompt, cancellationToken: cancellationToken);
            output = improvedResponse.Text ?? string.Empty;
        }

        return output;
    }

    private class CritiqueResult
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        
        [JsonPropertyName("feedback")]
        public string Feedback { get; set; } = string.Empty;
    }
}
