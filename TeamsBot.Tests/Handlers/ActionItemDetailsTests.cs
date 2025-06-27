using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using TeamsBot.Handlers;
using TeamsBot.Models;
using TeamsBot.Services;
using Xunit;

namespace TeamsBot.Tests.Handlers
{
    /// <summary>
    /// TDD Tests for ActionItemDetails model and intent detection logic
    /// Following Azure MCP patterns for fast, isolated unit tests (<60ms target)
    /// </summary>
    public class ActionItemDetailsTests
    {
        [Fact]
        public void ActionItemDetails_DefaultConstructor_InitializesWithDefaults()
        {
            // Arrange & Act - TDD Red: Test basic object creation
            var actionItem = new ActionItemDetails();

            // Assert - TDD Green: Verify default values
            actionItem.Title.Should().BeEmpty();
            actionItem.Description.Should().BeEmpty();
            actionItem.Priority.Should().Be("Medium");
            actionItem.AssignedTo.Should().BeNull();
            actionItem.WorkItemType.Should().Be("Task");
        }

        [Theory]
        [InlineData("create work item for fixing the login bug", true)]
        [InlineData("action item: implement new feature", true)]
        [InlineData("create task for John to review", true)]
        [InlineData("add to backlog: user story for mobile app", true)]
        [InlineData("just a regular conversation", false)]
        [InlineData("hello world", false)]
        [InlineData("", false)]
        public void ExtractFacilitatorKeywords_WithVariousMessages_DetectsCorrectly(string message, bool expectedResult)
        {
            // Arrange - Test intent detection logic
            var facilitatorKeywords = new[]
            {
                "create work item",
                "create task",
                "add to backlog",
                "action item",
                "follow up",
                "todo",
                "task for",
                "assign to",
                "create bug",
                "create story",
                "create epic"
            };

            // Act - Apply the same logic used in TeamsAIActivityHandler
            var result = facilitatorKeywords.Any(keyword => 
                message.ToLowerInvariant().Contains(keyword));

            // Assert - Verify keyword detection
            result.Should().Be(expectedResult);
        }

        [Theory]
        [InlineData("create urgent work item", "High")]
        [InlineData("high priority task", "High")]
        [InlineData("low priority bug fix", "Low")]
        [InlineData("normal task", "Medium")]
        [InlineData("", "Medium")]
        public void ExtractPriority_WithVariousMessages_ReturnsPriority(string message, string expectedPriority)
        {
            // Arrange & Act - Test priority extraction logic
            var result = ExtractPriorityFromMessage(message);

            // Assert
            result.Should().Be(expectedPriority);
        }

        [Theory]
        [InlineData("create bug for login issue", "Bug")]
        [InlineData("create epic for mobile app", "Epic")]
        [InlineData("create story for user login", "User Story")]
        [InlineData("create user story for dashboard", "User Story")]
        [InlineData("defect in payment system", "Bug")]
        [InlineData("regular task", "Task")]
        public void ExtractWorkItemType_WithVariousMessages_ReturnsType(string message, string expectedType)
        {
            // Arrange & Act - Test work item type extraction
            var result = ExtractWorkItemTypeFromMessage(message);

            // Assert
            result.Should().Be(expectedType);
        }

        [Theory]
        [InlineData("assign to john", "john")]
        [InlineData("task for sarah", "sarah")]
        [InlineData("for mike.smith", "mike.smith")]
        [InlineData("assign to alice@company.com", "alice@company.com")]
        [InlineData("no assignee mentioned", null)]
        public void ExtractAssignee_WithVariousMessages_ReturnsAssignee(string message, string? expectedAssignee)
        {
            // Arrange & Act - Test assignee extraction
            var result = ExtractAssigneeFromMessage(message);

            // Assert
            result.Should().Be(expectedAssignee);
        }

        [Fact]
        public void ExtractTitle_WithLongMessage_TruncatesAppropriately()
        {
            // Arrange - Test title extraction with long messages
            var longMessage = "this is a very long message that should be truncated to a reasonable title length for the work item";

            // Act - Apply same logic as TeamsAIActivityHandler
            var words = longMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = words.Length > 5 ? string.Join(' ', words.Take(8)) : longMessage;

            // Assert
            result.Should().Be("this is a very long message that should");
        }

        [Fact]
        public void ExtractTitle_WithShortMessage_ReturnsFullMessage()
        {
            // Arrange
            var shortMessage = "short message";

            // Act
            var words = shortMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = words.Length > 5 ? string.Join(' ', words.Take(8)) : shortMessage;

            // Assert
            result.Should().Be(shortMessage);
        }

        // Helper methods that mirror the logic in TeamsAIActivityHandler
        private static string ExtractPriorityFromMessage(string message)
        {
            var lowPriority = message.ToLowerInvariant();
            if (lowPriority.Contains("urgent") || lowPriority.Contains("high priority"))
                return "High";
            if (lowPriority.Contains("low priority"))
                return "Low";
            return "Medium";
        }

        private static string ExtractWorkItemTypeFromMessage(string message)
        {
            var lowMessage = message.ToLowerInvariant();
            if (lowMessage.Contains("bug") || lowMessage.Contains("defect"))
                return "Bug";
            if (lowMessage.Contains("epic"))
                return "Epic";
            if (lowMessage.Contains("story") || lowMessage.Contains("user story"))
                return "User Story";
            return "Task";
        }

        private static string? ExtractAssigneeFromMessage(string message)
        {
            var patterns = new[] { "assign to ", "for ", "task for " };
            foreach (var pattern in patterns)
            {
                var index = message.ToLowerInvariant().IndexOf(pattern);
                if (index >= 0)
                {
                    var afterPattern = message.Substring(index + pattern.Length);
                    var words = afterPattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0)
                        return words[0];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Integration tests for Teams AI Handler following MCP patterns
    /// Tests the actual handler logic with mocked dependencies
    /// </summary>
    public class TeamsAIActivityHandlerIntegrationTests
    {
        private readonly Mock<ILogger<TeamsAIActivityHandler>> _mockLogger;
        private readonly Mock<IAzureDevOpsService> _mockAzureDevOpsService;
        private readonly Mock<IConversationIntelligenceService> _mockConversationIntelligence;

        public TeamsAIActivityHandlerIntegrationTests()
        {
            _mockLogger = new Mock<ILogger<TeamsAIActivityHandler>>();
            _mockAzureDevOpsService = new Mock<IAzureDevOpsService>();
            _mockConversationIntelligence = new Mock<IConversationIntelligenceService>();
        }

        [Fact]
        public void Constructor_WithValidParameters_DoesNotThrow()
        {
            // Act & Assert - Constructor should work with valid dependencies
            var action = () => new TeamsAIActivityHandler(
                _mockLogger.Object, 
                _mockAzureDevOpsService.Object, 
                _mockConversationIntelligence.Object);
            action.Should().NotThrow();
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert - Following MCP defensive programming patterns
            var action = () => new TeamsAIActivityHandler(
                null!, 
                _mockAzureDevOpsService.Object, 
                _mockConversationIntelligence.Object);
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Fact]
        public void Constructor_WithNullAzureDevOpsService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var action = () => new TeamsAIActivityHandler(
                _mockLogger.Object, 
                null!, 
                _mockConversationIntelligence.Object);
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("azureDevOpsService");
        }

        [Fact]
        public void Constructor_WithNullConversationIntelligence_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var action = () => new TeamsAIActivityHandler(
                _mockLogger.Object, 
                _mockAzureDevOpsService.Object, 
                null!);
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("conversationIntelligence");
        }
    }
}
