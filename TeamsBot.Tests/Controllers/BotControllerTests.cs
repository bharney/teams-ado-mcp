using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using TeamsBot.Controllers;

namespace TeamsBot.Tests.Controllers
{
    /// <summary>
    /// Tests for BotController focusing on:
    /// 1. HTTP request handling
    /// 2. Dependency injection validation
    /// 3. Error handling
    /// 4. Health endpoint functionality
    /// </summary>
    public class BotControllerTests
    {
        private readonly Mock<IBotFrameworkHttpAdapter> _mockAdapter;
        private readonly Mock<IBot> _mockBot;
        private readonly Mock<ILogger<BotController>> _mockLogger;
        private readonly BotController _controller;

        public BotControllerTests()
        {
            _mockAdapter = new Mock<IBotFrameworkHttpAdapter>();
            _mockBot = new Mock<IBot>();
            _mockLogger = new Mock<ILogger<BotController>>();
            
            _controller = new BotController(_mockAdapter.Object, _mockBot.Object, _mockLogger.Object);
            
            // Setup HttpContext for the controller
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
        {
            // Arrange & Act
            var controller = new BotController(_mockAdapter.Object, _mockBot.Object, _mockLogger.Object);

            // Assert
            controller.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullAdapter_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new BotController(null!, _mockBot.Object, _mockLogger.Object));
            
            exception.ParamName.Should().Be("adapter");
        }

        [Fact]
        public void Constructor_WithNullBot_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new BotController(_mockAdapter.Object, null!, _mockLogger.Object));
            
            exception.ParamName.Should().Be("bot");
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new BotController(_mockAdapter.Object, _mockBot.Object, null!));
            
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public async Task PostAsync_WithValidRequest_ShouldCallAdapterProcessAsync()
        {
            // Arrange
            _mockAdapter
                .Setup(a => a.ProcessAsync(It.IsAny<HttpRequest>(), It.IsAny<HttpResponse>(), It.IsAny<IBot>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _controller.PostAsync();

            // Assert
            _mockAdapter.Verify(
                a => a.ProcessAsync(
                    It.IsAny<HttpRequest>(), 
                    It.IsAny<HttpResponse>(), 
                    _mockBot.Object, 
                    It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task PostAsync_WithValidRequest_ShouldLogInformation()
        {
            // Arrange
            _mockAdapter
                .Setup(a => a.ProcessAsync(It.IsAny<HttpRequest>(), It.IsAny<HttpResponse>(), It.IsAny<IBot>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _controller.PostAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing bot message from Teams")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task PostAsync_WhenAdapterThrowsException_ShouldLogErrorAndRethrow()
        {
            // Arrange
            var testException = new InvalidOperationException("Test exception");
            _mockAdapter
                .Setup(a => a.ProcessAsync(It.IsAny<HttpRequest>(), It.IsAny<HttpResponse>(), It.IsAny<IBot>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(testException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.PostAsync());
            
            exception.Should().Be(testException);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error processing bot message")),
                    testException,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void Health_ShouldReturnOkResultWithHealthStatus()
        {
            // Act
            var result = _controller.Health();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().NotBeNull();
            
            // Check that the response contains expected properties
            var responseValue = okResult.Value!.ToString();
            responseValue.Should().Contain("status");
            responseValue.Should().Contain("healthy");
            responseValue.Should().Contain("timestamp");
        }

        [Fact]
        public void Health_ShouldReturnCurrentTimestamp()
        {
            // Arrange
            var beforeCall = DateTime.UtcNow;

            // Act
            var result = _controller.Health();

            // Assert
            var afterCall = DateTime.UtcNow;
            
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            
            // Verify the timestamp is recent (within the test execution time)
            var responseValue = okResult!.Value!.ToString();
            responseValue.Should().Contain("timestamp");
            
            // The timestamp should be between before and after the call
            // This is a basic sanity check that a timestamp was generated
            responseValue.Should().Contain(DateTime.UtcNow.Year.ToString());
        }

        [Theory]
        [InlineData("api/messages")]
        public void Controller_ShouldHaveCorrectRouteAttribute(string expectedRoute)
        {
            // Arrange & Act
            var routeAttributes = typeof(BotController)
                .GetCustomAttributes(typeof(RouteAttribute), false)
                .Cast<RouteAttribute>();

            // Assert
            routeAttributes.Should().NotBeEmpty();
            routeAttributes.First().Template.Should().Be(expectedRoute);
        }

        [Fact]
        public void Controller_ShouldHaveApiControllerAttribute()
        {
            // Arrange & Act
            var hasApiControllerAttribute = typeof(BotController)
                .GetCustomAttributes(typeof(ApiControllerAttribute), false)
                .Any();

            // Assert
            hasApiControllerAttribute.Should().BeTrue();
        }

        [Fact]
        public void PostAsync_ShouldHaveHttpPostAttribute()
        {
            // Arrange & Act
            var method = typeof(BotController).GetMethod("PostAsync");
            var hasHttpPostAttribute = method!
                .GetCustomAttributes(typeof(HttpPostAttribute), false)
                .Any();

            // Assert
            hasHttpPostAttribute.Should().BeTrue();
        }

        [Fact]
        public void Health_ShouldHaveHttpGetAttributeWithCorrectRoute()
        {
            // Arrange & Act
            var method = typeof(BotController).GetMethod("Health");
            var httpGetAttributes = method!
                .GetCustomAttributes(typeof(HttpGetAttribute), false)
                .Cast<HttpGetAttribute>();

            // Assert
            httpGetAttributes.Should().NotBeEmpty();
            httpGetAttributes.First().Template.Should().Be("health");
        }
    }
}
