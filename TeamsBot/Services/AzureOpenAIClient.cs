using Azure.AI.OpenAI; // 2.0 SDK (top-level AzureOpenAIClient)
using OpenAI.Chat; // Chat types from OpenAI namespace (ChatClient, ChatMessage, ChatCompletion, ChatCompletionOptions)
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeamsBot.Configuration;

namespace TeamsBot.Services;

public interface IAzureOpenAIClient
{
    Task<ChatCompletion> CompleteChatAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default);
}

// Wrapper around Azure.AI.OpenAI 2.x ChatClient for simplified DI & options usage
public class AzureOpenAIClient : IAzureOpenAIClient
{
    private readonly ILogger<AzureOpenAIClient> _logger;
    private readonly AzureOpenAIOptions _options;
    private readonly ChatClient _chatClient;

    public AzureOpenAIClient(ILogger<AzureOpenAIClient> logger, IOptions<AzureOpenAIOptions> options, TokenCredential credential)
    {
        _logger = logger; _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
            throw new InvalidOperationException("AzureOpenAI endpoint not configured");
        // 2.x pattern: top-level AzureOpenAIClient then specialized ChatClient
    var topLevel = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(_options.Endpoint), credential);
        _chatClient = topLevel.GetChatClient(_options.ChatDeployment);
    }

    public async Task<ChatCompletion> CompleteChatAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        var list = messages.ToList();
        _logger.LogDebug("Sending {Count} messages to Azure OpenAI deployment {Deployment}", list.Count, _options.ChatDeployment);
        // TODO: When temperature / max tokens option properties are documented, reintroduce options configuration.
        var completion = await _chatClient.CompleteChatAsync(list, cancellationToken: ct);
        return completion;
    }
}
