using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using TeamsBot.Services;
using TeamsBot.Configuration;
using TeamsBot.Models;
using Xunit;

namespace TeamsBot.Tests.Services
{
    /// <summary>
    /// TDD Tests for Azure DevOps Service following Azure MCP patterns
    /// Validates API interactions, authentication, and error handling
    /// Performance target: <60ms for L0 tests per MCP guidelines
    /// </summary>
    public class AzureDevOpsServiceTests
    {
        private readonly Mock<ILogger<AzureDevOpsService>> _mockLogger;
        private readonly Mock<ISecureConfigurationProvider> _mockConfigProvider;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;

        public AzureDevOpsServiceTests()
        {
            _mockLogger = new Mock<ILogger<AzureDevOpsService>>();
            _mockConfigProvider = new Mock<ISecureConfigurationProvider>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        }

        [Fact]
        public async Task CreateWorkItemAsync_WithValidActionItem_ReturnsWorkItemId()
        {
            // Arrange - TDD Red: Define expected behavior
            var actionItem = new ActionItemDetails
            {
                Title = "Test Work Item",
                Description = "Test Description",
                Priority = "High",
                WorkItemType = "Task"
            };

            var expectedWorkItemId = 12345;
            var expectedResponseContent = JsonSerializer.Serialize(new { Id = expectedWorkItemId, Url = "https://dev.azure.com/test" });

            // Mock configuration following MCP secure patterns
            var mockConfig = new AzureConfiguration
            {
                AzureDevOps = new AzureDevOpsConfiguration
                {
                    Organization = "test-org",
                    Project = "test-project",
                    PersonalAccessToken = "test-pat",
                    DefaultWorkItemType = "Task"
                }
            };

            _mockConfigProvider.Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConfig);

            // Mock HTTP response following MCP mocking patterns
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedResponseContent)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var service = new AzureDevOpsService(_httpClient, _mockLogger.Object, _mockConfigProvider.Object);

            // Act - TDD Green: Execute the method
            var result = await service.CreateWorkItemAsync(actionItem);

            // Assert - TDD Refactor: Validate all expected behaviors
            result.Should().Be(expectedWorkItemId);

            // Verify HTTP request was made with correct authentication
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("test-org") &&
                    req.RequestUri.ToString().Contains("test-project") &&
                    req.Headers.Authorization!.Scheme == "Basic"),
                ItExpr.IsAny<CancellationToken>());

            // Verify logging follows MCP patterns
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating work item: Test Work Item")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateWorkItemAsync_WithIncompleteConfiguration_ReturnsNull()
        {
            // Arrange - Test error handling for missing configuration
            var actionItem = new ActionItemDetails
            {
                Title = "Test Work Item",
                Description = "Test Description"
            };

            var mockConfig = new AzureConfiguration
            {
                AzureDevOps = new AzureDevOpsConfiguration
                {
                    // Missing Organization and Project - should fail gracefully
                    PersonalAccessToken = "test-pat"
                }
            };

            _mockConfigProvider.Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConfig);

            var service = new AzureDevOpsService(_httpClient, _mockLogger.Object, _mockConfigProvider.Object);

            // Act
            var result = await service.CreateWorkItemAsync(actionItem);

            // Assert - Should handle configuration errors gracefully
            result.Should().BeNull();

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Azure DevOps configuration is incomplete")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateWorkItemAsync_WithHttpError_ReturnsNullAndLogsError()
        {
            // Arrange - Test HTTP error handling following MCP resilience patterns
            var actionItem = new ActionItemDetails
            {
                Title = "Test Work Item",
                Description = "Test Description"
            };

            var mockConfig = new AzureConfiguration
            {
                AzureDevOps = new AzureDevOpsConfiguration
                {
                    Organization = "test-org",
                    Project = "test-project",
                    PersonalAccessToken = "test-pat"
                }
            };

            _mockConfigProvider.Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConfig);

            // Mock HTTP error response
            var errorResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Invalid request")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(errorResponse);

            var service = new AzureDevOpsService(_httpClient, _mockLogger.Object, _mockConfigProvider.Object);

            // Act
            var result = await service.CreateWorkItemAsync(actionItem);

            // Assert - Should handle HTTP errors gracefully
            result.Should().BeNull();

            // Verify error logging with status code
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to create work item") && 
                                                  v.ToString()!.Contains("BadRequest")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetWorkItemAsync_WithValidId_ReturnsWorkItemInfo()
        {
            // Arrange - Test work item retrieval
            var workItemId = 12345;
            var expectedResponse = JsonSerializer.Serialize(new
            {
                Id = workItemId,
                Url = "https://dev.azure.com/test/_workitems/edit/12345",
                Fields = new
                {
                    Title = "Test Work Item",
                    State = "New",
                    AssignedTo = "test@example.com"
                }
            });

            var mockConfig = new AzureConfiguration
            {
                AzureDevOps = new AzureDevOpsConfiguration
                {
                    Organization = "test-org",
                    PersonalAccessToken = "test-pat"
                }
            };

            _mockConfigProvider.Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConfig);

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedResponse)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var service = new AzureDevOpsService(_httpClient, _mockLogger.Object, _mockConfigProvider.Object);

            // Act
            var result = await service.GetWorkItemAsync(workItemId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(workItemId);
            result.Title.Should().Be("Test Work Item");
            result.State.Should().Be("New");
            result.AssignedTo.Should().Be("test@example.com");
        }

        [Theory]
        [InlineData("High", 1)]
        [InlineData("Medium", 2)]
        [InlineData("Low", 3)]
        [InlineData("Unknown", 2)] // Default to Medium
        public async Task CreateWorkItemAsync_WithDifferentPriorities_SetsPriorityCorrectly(string priority, int expectedPriorityValue)
        {
            // Arrange - Test priority mapping logic
            var actionItem = new ActionItemDetails
            {
                Title = "Priority Test",
                Priority = priority,
                WorkItemType = "Task"
            };

            var mockConfig = new AzureConfiguration
            {
                AzureDevOps = new AzureDevOpsConfiguration
                {
                    Organization = "test-org",
                    Project = "test-project",
                    PersonalAccessToken = "test-pat"
                }
            };

            _mockConfigProvider.Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConfig);

            var expectedWorkItemId = 12345;
            var responseContent = JsonSerializer.Serialize(new { Id = expectedWorkItemId });

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            };

            string? capturedRequestBody = null;
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    capturedRequestBody = request.Content?.ReadAsStringAsync().Result;
                })
                .ReturnsAsync(responseMessage);

            var service = new AzureDevOpsService(_httpClient, _mockLogger.Object, _mockConfigProvider.Object);

            // Act
            var result = await service.CreateWorkItemAsync(actionItem);

            // Assert
            result.Should().Be(expectedWorkItemId);
            capturedRequestBody.Should().NotBeNull();
            capturedRequestBody.Should().Contain($"\"value\":{expectedPriorityValue}");
        }

        [Fact]
        public void Constructor_WithNullParameters_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert - Defensive programming following MCP patterns
            var action1 = () => new AzureDevOpsService(null!, _mockLogger.Object, _mockConfigProvider.Object);
            var action2 = () => new AzureDevOpsService(_httpClient, null!, _mockConfigProvider.Object);
            var action3 = () => new AzureDevOpsService(_httpClient, _mockLogger.Object, null!);

            action1.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
            action2.Should().Throw<ArgumentNullException>().WithParameterName("logger");
            action3.Should().Throw<ArgumentNullException>().WithParameterName("configurationProvider");
        }

        [Fact]
        public async Task CreateWorkItemAsync_WithAssignee_IncludesAssigneeInRequest()
        {
            // Arrange - Test assignee field handling
            var actionItem = new ActionItemDetails
            {
                Title = "Assigned Work Item",
                AssignedTo = "john.doe@example.com",
                WorkItemType = "Task"
            };

            var mockConfig = new AzureConfiguration
            {
                AzureDevOps = new AzureDevOpsConfiguration
                {
                    Organization = "test-org",
                    Project = "test-project",
                    PersonalAccessToken = "test-pat"
                }
            };

            _mockConfigProvider.Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConfig);

            var responseContent = JsonSerializer.Serialize(new { Id = 12345 });
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            };

            string? capturedRequestBody = null;
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    capturedRequestBody = request.Content?.ReadAsStringAsync().Result;
                })
                .ReturnsAsync(responseMessage);

            var service = new AzureDevOpsService(_httpClient, _mockLogger.Object, _mockConfigProvider.Object);

            // Act
            await service.CreateWorkItemAsync(actionItem);

            // Assert - Verify assignee is included in request
            capturedRequestBody.Should().NotBeNull();
            capturedRequestBody.Should().Contain("System.AssignedTo");
            capturedRequestBody.Should().Contain("john.doe@example.com");
        }
    }
}
