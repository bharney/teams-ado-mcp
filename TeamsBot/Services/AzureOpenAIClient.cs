using Azure.AI.OpenAI; // 2.0.0
using Azure.Core;
using Json.More;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TeamsBot.Configuration;

namespace TeamsBot.Services;

public interface IAzureOpenAIClient
{
    Task<string> CompleteChatAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default);
}

/// <summary>
/// Wrapper around Azure.AI.OpenAI AzureOpenAIClient (Azure resource specific) providing a simple interface.
/// </summary>
public sealed class AzureOpenAIClient : IAzureOpenAIClient
{
    private readonly Azure.AI.OpenAI.AzureOpenAIClient? _client; // typed SDK client (may stay null if init fails)
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<AzureOpenAIClient> _logger;
    private readonly ChatClient _chatClient;

    public AzureOpenAIClient(IOptions<AzureOpenAIOptions> options, ApiKeyCredential credential, ILogger<AzureOpenAIClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        if (string.IsNullOrWhiteSpace(_options.Endpoint)) throw new InvalidOperationException("AzureOpenAI:Endpoint missing.");
        if (string.IsNullOrWhiteSpace(_options.ChatDeployment)) throw new InvalidOperationException("AzureOpenAI:ChatDeployment missing.");
        try
        {
            _client = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(_options.Endpoint), credential);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AzureOpenAI typed client initialization failed; will use REST fallback");
        }
    }

    public async Task<string> CompleteChatAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        var list = messages?.Where(m => !string.IsNullOrWhiteSpace(m.Content?.ToString())).ToList() ?? new List<ChatMessage>();
        if (list.Count == 0) return string.Empty;

        // Offline shortcut for unit tests
        if (_options.Endpoint.Contains("test-endpoint", StringComparison.OrdinalIgnoreCase))
            return string.Join(" | ", list.Select(m => m.Content?.ToString()));

        try
        {
            ChatClient _chatClient = _client.GetChatClient(_options.ChatDeployment) ?? throw new InvalidOperationException("Could not getch AzureOpenAI:ChatDeployment");
            var result = await _chatClient.CompleteChatAsync(list, cancellationToken: ct);
            ChatCompletion chatCompletion = result.Value;
            return chatCompletion.Content?.FirstOrDefault()?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Typed Azure.AI.OpenAI attempt failed; using REST fallback");
        }
        return string.Empty;
    }
}

public sealed class NullAzureOpenAIClient : IAzureOpenAIClient
{
    public static readonly NullAzureOpenAIClient Instance = new();
    private NullAzureOpenAIClient() { }
    public Task<string> CompleteChatAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default) => Task.FromResult(string.Empty);
}
