using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Azure.AI.OpenAI;
using OpenAI.Chat; // chat types
using TeamsBot.Services; // wrapper client
using TeamsBot.Configuration;
using Microsoft.Extensions.Options;
using Azure.Core;
using Azure;
using System;
using System.Collections.Generic;

namespace TeamsBot.Tests.Services
{
    using WrapperAzureOpenAIClient = TeamsBot.Services.AzureOpenAIClient;
    using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

    public class AzureOpenAIClientTests
    {
        private WrapperAzureOpenAIClient CreateClient(AzureOpenAIOptions opts, TokenCredential credential)
        {
            var logger = Mock.Of<ILogger<WrapperAzureOpenAIClient>>();
            return new WrapperAzureOpenAIClient(logger, Options.Create(opts), credential);
        }

        private class DummyCredential : TokenCredential
        {
            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
                => new AccessToken("dummy", DateTimeOffset.UtcNow.AddMinutes(5));
            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
                => new ValueTask<AccessToken>(new AccessToken("dummy", DateTimeOffset.UtcNow.AddMinutes(5)));
        }

        [Fact(Skip = "Requires live Azure OpenAI deployment; integration test handled elsewhere")] 
        public async Task GetChatCompletionsAsync_ShouldReturnResult_WhenDeploymentConfigured()
        {
            var opts = new AzureOpenAIOptions
            {
                Endpoint = Environment.GetEnvironmentVariable("TEST_AZURE_OPENAI_ENDPOINT") ?? string.Empty,
                ChatDeployment = Environment.GetEnvironmentVariable("TEST_AZURE_OPENAI_CHAT_DEPLOYMENT") ?? string.Empty,
                Enabled = true
            };
            if (string.IsNullOrWhiteSpace(opts.Endpoint) || string.IsNullOrWhiteSpace(opts.ChatDeployment))
                return; // skip silently if not configured

            var client = CreateClient(opts, new DummyCredential());
            var messages = new OpenAIChatMessage[]
            {
                OpenAIChatMessage.CreateSystemMessage("You are a test system message."),
                OpenAIChatMessage.CreateUserMessage("Return JSON {\"ping\":true}")
            };

            var result = await client.CompleteChatAsync(messages);
            result.Should().NotBeNull();
            result.Content.Should().NotBeEmpty();
        }

        [Fact]
        public void Constructor_ShouldThrow_WhenEndpointMissing()
        {
            var opts = new AzureOpenAIOptions
            {
                Endpoint = " ",
                ChatDeployment = "dep"
            };
            Action act = () => CreateClient(opts, new DummyCredential());
            act.Should().Throw<InvalidOperationException>().WithMessage("*endpoint not configured*");
        }
    }
}
