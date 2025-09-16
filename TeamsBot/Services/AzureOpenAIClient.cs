// Temporarily disabling Azure OpenAI strong types pending correct 2.x SDK integration to unblock build
// using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeamsBot.Configuration;

namespace TeamsBot.Services;

public interface IAzureOpenAIClient
{
  Task<string> CompleteChatAsync(IEnumerable<string> messages, CancellationToken ct = default);
}

// Wrapper around Azure.AI.OpenAI 2.x ChatClient for simplified DI & options usage
public class AzureOpenAIClient : IAzureOpenAIClient
{
  private readonly ILogger<AzureOpenAIClient> _logger;
  private readonly AzureOpenAIOptions _options;
  // private readonly ChatClient _chatClient;

  public AzureOpenAIClient(ILogger<AzureOpenAIClient> logger, IOptions<AzureOpenAIOptions> options, TokenCredential credential)
  {
    _logger = logger; _options = options.Value;
    if (string.IsNullOrWhiteSpace(_options.Endpoint))
      throw new InvalidOperationException("AzureOpenAI endpoint not configured");
    // TODO: Reintroduce real client once types resolved
  }

  public Task<string> CompleteChatAsync(IEnumerable<string> messages, CancellationToken ct = default)
  {
    // Stub: join messages, return placeholder
    var joined = string.Join("\n", messages);
    _logger.LogInformation("Stub OpenAI client invoked with {Length} chars", joined.Length);
    return Task.FromResult("{\"stub\":true,\"reasoning\":\"AI disabled\"}");
  }
}
