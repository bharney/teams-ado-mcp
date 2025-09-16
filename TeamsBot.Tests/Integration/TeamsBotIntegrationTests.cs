using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TeamsBot.Handlers;

namespace TeamsBot.Tests.Integration
{
    /// <summary>
    /// Integration tests for the entire TeamsBot application
    /// Tests the full request pipeline from HTTP endpoint to bot logic
    /// </summary>
    public class TeamsBotIntegrationTests : IClassFixture<WebApplicationFactory<TeamsBot.Program>>
    {
        private readonly WebApplicationFactory<TeamsBot.Program> _factory;
        private readonly HttpClient _client;

        public TeamsBotIntegrationTests(WebApplicationFactory<TeamsBot.Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    // Override services for testing if needed
                    services.AddLogging(logging => logging.AddConsole());
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task HealthEndpoint_ShouldReturnHealthyStatus()
        {
            // Act
            var response = await _client.GetAsync("/api/messages/health");

            // Assert
            response.Should().BeSuccessful();

            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("healthy");
            content.Should().Contain("timestamp");
        }

        [Fact]
        public async Task HealthEndpoint_ShouldReturnJsonResponse()
        {
            // Act
            var response = await _client.GetAsync("/api/messages/health");

            // Assert
            response.Should().BeSuccessful();
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

            var jsonContent = await response.Content.ReadAsStringAsync();
            var healthResponse = JsonSerializer.Deserialize<HealthResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            healthResponse.Should().NotBeNull();
            healthResponse!.Status.Should().Be("healthy");
            healthResponse.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Fact]
        public void ServiceRegistration_ShouldContainRequiredServices()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();

            // Act & Assert
            scope.ServiceProvider.GetService<IBotFrameworkHttpAdapter>().Should().NotBeNull();
            scope.ServiceProvider.GetService<IBot>().Should().NotBeNull();
            scope.ServiceProvider.GetService<IBot>().Should().BeOfType<TeamsAIActivityHandler>();
        }

        [Fact]
        public void ServiceRegistration_ShouldRegisterTeamsAIActivityHandler()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();

            // Act
            var bot = scope.ServiceProvider.GetService<IBot>();

            // Assert
            bot.Should().NotBeNull();
            bot.Should().BeOfType<TeamsAIActivityHandler>();
        }

        [Fact]
        public void ServiceRegistration_ShouldRegisterBotFrameworkAdapter()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();

            // Act
            var adapter = scope.ServiceProvider.GetService<IBotFrameworkHttpAdapter>();

            // Assert
            adapter.Should().NotBeNull();
            adapter.Should().BeOfType<AdapterWithErrorHandler>();
        }

        [Fact]
        public async Task BotEndpoint_WithoutBotFrameworkAuth_ShouldReturn401()
        {
            // Arrange
            var botMessage = new
            {
                type = "message",
                id = "test-id",
                timestamp = DateTime.UtcNow,
                from = new { id = "user1", name = "Test User" },
                conversation = new { id = "conv1" },
                recipient = new { id = "bot", name = "Bot" },
                text = "Hello bot"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/messages", botMessage);

            // Assert
            // With no Bot Framework credentials supplied, endpoint should reject with 401 Unauthorized
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData("/api/messages/health")]
        [InlineData("/api/messages/Health")]
        [InlineData("/api/messages/HEALTH")]
        public async Task HealthEndpoint_WithDifferentCasing_ShouldWork(string endpoint)
        {
            // Act
            var response = await _client.GetAsync(endpoint);

            // Assert
            response.Should().BeSuccessful();
        }

        [Fact]
        public async Task Application_ShouldStartSuccessfully()
        {
            // Act & Assert
            // If the factory can create a client, the application started successfully
            _client.Should().NotBeNull();

            // Verify basic connectivity
            var response = await _client.GetAsync("/api/messages/health");
            response.Should().BeSuccessful();
        }

        [Fact]
        public void Configuration_ShouldLoadCorrectly()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();

            // Act
            var config = scope.ServiceProvider.GetService<Microsoft.Extensions.Configuration.IConfiguration>();

            // Assert
            config.Should().NotBeNull();

            // Verify some basic configuration sections exist
            config.GetSection("Logging").Should().NotBeNull();
        }

        private class HealthResponse
        {
            public string Status { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
    }
}
