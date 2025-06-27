using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using TeamsBot.Handlers;

namespace TeamsBot.Tests.Handlers
{
    /// <summary>
    /// Simplified tests for MinimalTeamsActivityHandler focusing on:
    /// 1. Constructor validation
    /// 2. Basic functionality verification
    /// </summary>
    public class MinimalTeamsActivityHandlerTests
    {
        private readonly Mock<ILogger<MinimalTeamsActivityHandler>> _mockLogger;
        private readonly MinimalTeamsActivityHandler _handler;

        public MinimalTeamsActivityHandlerTests()
        {
            _mockLogger = new Mock<ILogger<MinimalTeamsActivityHandler>>();
            _handler = new MinimalTeamsActivityHandler(_mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidLogger_ShouldInitializeSuccessfully()
        {
            // Arrange & Act
            var handler = new MinimalTeamsActivityHandler(_mockLogger.Object);

            // Assert
            handler.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new MinimalTeamsActivityHandler(null!));
            
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Handler_ShouldInheritFromActivityHandler()
        {
            // Arrange & Act & Assert
            _handler.Should().BeAssignableTo<Microsoft.Bot.Builder.ActivityHandler>();
        }

        [Fact]
        public void Handler_ShouldImplementIBot()
        {
            // Arrange & Act & Assert
            _handler.Should().BeAssignableTo<Microsoft.Bot.Builder.IBot>();
        }
    }
}
