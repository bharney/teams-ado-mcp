using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using Moq;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeamsBot.Configuration;
using TeamsBot.Services; // wrapper client
using Xunit;

namespace TeamsBot.Tests.Services
{
    using WrapperAzureOpenAIClient = TeamsBot.Services.AzureOpenAIClient;

    public class AzureOpenAIClientTests
    {
        private WrapperAzureOpenAIClient CreateClient(AzureOpenAIOptions opts, ApiKeyCredential credential)
        {
            var logger = Mock.Of<ILogger<WrapperAzureOpenAIClient>>();
            return new WrapperAzureOpenAIClient(Options.Create(opts), credential, logger);
        }

        private class DummyCredential : ApiKeyCredential
        {
            public DummyCredential() : base("dummy") { }
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
            var chat = new OpenAI.Chat.ChatMessage[]
            {
                OpenAI.Chat.ChatMessage.CreateSystemMessage("system"),
                OpenAI.Chat.ChatMessage.CreateUserMessage("user")
             };

            var result = await client.CompleteChatAsync(chat);
            result.Should().NotBeNull();
            result.Should().NotBeNullOrEmpty();
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
            act.Should().Throw<InvalidOperationException>().WithMessage("*Endpoint missing*");
        }
    }
}
