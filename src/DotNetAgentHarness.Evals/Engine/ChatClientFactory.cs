using System;
using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace DotNetAgentHarness.Evals.Engine;

public sealed class ChatClientFactoryOptions
{
    public string Provider { get; init; } = "openai";
    public string Model { get; init; } = "gpt-4.1-mini";
    public string? ApiKey { get; init; }
    public string? Endpoint { get; init; }
}

public static class ChatClientFactory
{
    public static IChatClient Create(ChatClientFactoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            throw new InvalidOperationException("Eval provider is required. Set DOTNET_AGENT_HARNESS_EVAL_PROVIDER or use --provider.");
        }

        return options.Provider.Trim().ToLowerInvariant() switch
        {
            "openai" => CreateOpenAiClient(options),
            _ => throw new InvalidOperationException(
                $"Eval provider '{options.Provider}' is not supported. Supported providers: openai.")
        };
    }

    private static IChatClient CreateOpenAiClient(ChatClientFactoryOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key missing. Set EVAL_OPENAI_KEY (or OPENAI_API_KEY) when running in real mode.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException("OpenAI model is required. Set EVAL_OPENAI_MODEL or use --model.");
        }

        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            clientOptions.Endpoint = new Uri(options.Endpoint, UriKind.Absolute);
        }

        var openAiClient = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        return openAiClient.GetChatClient(options.Model).AsIChatClient();
    }
}
