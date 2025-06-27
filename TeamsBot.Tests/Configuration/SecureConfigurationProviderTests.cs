using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Azure.Identity;
using TeamsBot.Configuration;

namespace TeamsBot.Tests.Configuration
{
    /// <summary>
    /// Tests for SecureConfigurationProvider following Azure MCP TDD patterns
    /// Validates SFI-compliant authentication and credential management
    /// </summary>
    public class SecureConfigurationProviderTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<SecureConfigurationProvider>> _mockLogger;
        private readonly DefaultAzureCredential _credential;

        public SecureConfigurationProviderTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<SecureConfigurationProvider>>();
            _credential = new DefaultAzureCredential();
        }

        [Fact]
        public async Task GetSecretAsync_WithValidConfiguration_ReturnsSecret()
        {
            // Arrange - TDD Red: Define what should happen
            var secretName = "TestSecret";
            var expectedValue = "SecretValue123";
            
            _mockConfiguration.Setup(c => c[secretName])
                .Returns(expectedValue);

            var provider = new SecureConfigurationProvider(
                _mockConfiguration.Object,
                _mockLogger.Object,
                _credential);

            // Act - TDD Green: Implement minimal code to pass
            var result = await provider.GetSecretAsync(secretName);

            // Assert - TDD Refactor: Validate behavior
            result.Should().Be(expectedValue);
            
            // Verify logging follows Azure MCP patterns
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Retrieved secret {secretName} from configuration")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetSecretAsync_WithMissingSecret_ReturnsEmptyString()
        {
            // Arrange - Following MCP error handling patterns
            var secretName = "NonExistentSecret";
            
            _mockConfiguration.Setup(c => c[secretName])
                .Returns((string?)null);

            var provider = new SecureConfigurationProvider(
                _mockConfiguration.Object,
                _mockLogger.Object,
                _credential);

            // Act
            var result = await provider.GetSecretAsync(secretName);

            // Assert - Should gracefully handle missing secrets
            result.Should().BeEmpty();
            
            // Verify warning is logged as per MCP security guidelines
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Secret {secretName} not found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetConfigurationAsync_WithValidSettings_ReturnsConfiguration()
        {
            // Arrange - Test SFI-compliant configuration loading
            var configData = new Dictionary<string, string?>
            {
                ["AzureAd:TenantId"] = "test-tenant-id",
                ["AzureAd:ClientId"] = "test-client-id",
                ["MicrosoftAppType"] = "MultiTenant",
                ["MicrosoftAppId"] = "test-app-id",
                ["AzureDevOps:Organization"] = "test-org",
                ["AzureDevOps:Project"] = "test-project",
                ["McpServer:Url"] = "http://localhost:5000"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var provider = new SecureConfigurationProvider(
                configuration,
                _mockLogger.Object,
                _credential);

            // Act
            var result = await provider.GetConfigurationAsync();

            // Assert - Validate all configuration sections are populated
            result.Should().NotBeNull();
            result.AzureAd.TenantId.Should().Be("test-tenant-id");
            result.AzureAd.ClientId.Should().Be("test-client-id");
            result.Bot.MicrosoftAppType.Should().Be("MultiTenant");
            result.Bot.MicrosoftAppId.Should().Be("test-app-id");
            result.AzureDevOps.Organization.Should().Be("test-org");
            result.AzureDevOps.Project.Should().Be("test-project");
            result.McpServer.Url.Should().Be("http://localhost:5000");
            
            // Verify configuration loading is logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Configuration loaded successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert - Defensive programming following MCP patterns
            var action = () => new SecureConfigurationProvider(
                null!,
                _mockLogger.Object,
                _credential);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("configuration");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert - Ensure logging is always available
            var action = () => new SecureConfigurationProvider(
                _mockConfiguration.Object,
                null!,
                _credential);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        public void Constructor_WithNullCredential_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert - SFI compliance requires credential validation
            var action = () => new SecureConfigurationProvider(
                _mockConfiguration.Object,
                _mockLogger.Object,
                null!);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("credential");
        }

        [Fact]
        public async Task GetSecretAsync_WithException_ReturnsEmptyStringAndLogsError()
        {
            // Arrange - Test error resilience following MCP patterns
            var secretName = "FailingSecret";
            var expectedException = new InvalidOperationException("Test exception");
            
            _mockConfiguration.Setup(c => c[secretName])
                .Throws(expectedException);

            var provider = new SecureConfigurationProvider(
                _mockConfiguration.Object,
                _mockLogger.Object,
                _credential);

            // Act
            var result = await provider.GetSecretAsync(secretName);

            // Assert - Should handle exceptions gracefully
            result.Should().BeEmpty();
            
            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error retrieving secret {secretName}")),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(null)]
        public async Task GetSecretAsync_WithInvalidSecretName_ReturnsEmptyString(string? secretName)
        {
            // Arrange - Test input validation
            var provider = new SecureConfigurationProvider(
                _mockConfiguration.Object,
                _mockLogger.Object,
                _credential);

            // Act
            var result = await provider.GetSecretAsync(secretName!);

            // Assert - Should handle invalid input gracefully
            result.Should().BeEmpty();
        }
    }
}
