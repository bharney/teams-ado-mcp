using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeamsBot.Configuration;
using TeamsBot.Services;
using Xunit;
using Azure.Core;
using Azure.Identity;
using OpenAI.Chat;
using System.ClientModel;

namespace TeamsBot.Tests;

public class AzureOpenAIClientTests
{
    // Remove StubCredential, not needed for ApiKeyCredential-based constructor

    private sealed class StubApiKeyCredential : ApiKeyCredential
    {
        public StubApiKeyCredential() : base("stub-key") { }
    }

    private static IOptions<AzureOpenAIOptions> MakeOptions(bool enabled = true, string? endpoint = "https://test-endpoint.openai.azure.com/", string? deployment = "gpt-test") => Options.Create(new AzureOpenAIOptions
    {
        Enabled = enabled,
        Endpoint = endpoint ?? string.Empty,
        ChatDeployment = deployment ?? string.Empty,
        MaxOutputTokens = 32,
        Temperature = 0.1f
    });

    [Fact]
    public void Ctor_ShouldThrow_WhenEndpointMissing()
    {
        var opts = MakeOptions(endpoint: "");
        var act = () => new AzureOpenAIClient(options: opts, credential: new StubApiKeyCredential(), logger: NullLogger<AzureOpenAIClient>.Instance);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenDeploymentMissing()
    {
        var opts = MakeOptions(deployment: "");
        var act = () => new AzureOpenAIClient(options: opts, credential: new StubApiKeyCredential(), logger: NullLogger<AzureOpenAIClient>.Instance);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task NullClient_ShouldReturnEmpty()
    {
        var result = await NullAzureOpenAIClient.Instance.CompleteChatAsync(new[] { ChatMessage.CreateUserMessage("x") });
        result.Should().BeEmpty();
    }
}
