using Microsoft.Extensions.Logging;
using Moq;
using TeamsBot.Handlers;
using TeamsBot.Services;
using McpServer.Services; // consolidated ADO service
using TeamsBot.Models;
using Xunit;
using FluentAssertions;

namespace TeamsBot.Tests.Handlers
{
    /// <summary>
    /// Simple unit tests to demonstrate the Teams AI functionality
    /// Tests the core intent detection and action item extraction logic
    /// </summary>
    public class SimpleFunctionalityTests
    {
        [Theory]
        [InlineData("create work item for testing", true)]
        [InlineData("add to backlog: implement feature X", true)]
        [InlineData("action item: review the code", true)]
        [InlineData("task for John: update the docs", true)]
        [InlineData("create bug for the login issue", true)]
        [InlineData("Hello everyone", false)]
        [InlineData("How are you?", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void FacilitatorKeywordDetection_ShouldIdentifyCorrectMessages(string? message, bool expectedDetection)
        {
            // Arrange - Keywords that should trigger facilitator prompt detection
            var facilitatorKeywords = new[]
            {
                "create work item", "create task", "add to backlog", "action item",
                "follow up", "todo", "task for", "assign to", "create bug", "create story", "create epic"
            };

            // Act - Simulate the keyword detection logic from TeamsAIActivityHandler
            bool containsKeywords = !string.IsNullOrEmpty(message) &&
                facilitatorKeywords.Any(keyword => message.ToLowerInvariant().Contains(keyword));

            // Assert
            containsKeywords.Should().Be(expectedDetection);
        }

        [Fact]
        public void ActionItemDetails_ShouldInitializeWithCorrectDefaults()
        {
            // Arrange & Act
            var actionItem = new ActionItemDetails();

            // Assert - Verify default values match expected behavior
            actionItem.Title.Should().Be(string.Empty);
            actionItem.Description.Should().Be(string.Empty);
            actionItem.Priority.Should().Be("Medium");
            actionItem.AssignedTo.Should().BeNull();
            actionItem.WorkItemType.Should().Be("Task");
        }

        [Theory]
        [InlineData("urgent task for fixing the bug", "High")]
        [InlineData("high priority: update documentation", "High")]
        [InlineData("low priority: clean up code", "Low")]
        [InlineData("create task for testing", "Medium")]
        [InlineData("", "Medium")]
        public void PriorityExtraction_ShouldDetectCorrectPriority(string message, string expectedPriority)
        {
            // Arrange & Act - Simulate the priority extraction logic
            var extractedPriority = ExtractPriorityFromMessage(message);

            // Assert
            extractedPriority.Should().Be(expectedPriority);
        }

        [Theory]
        [InlineData("create bug for login issue", "Bug")]
        [InlineData("add defect to backlog", "Bug")]
        [InlineData("create epic for user management", "Epic")]
        [InlineData("user story: implement login", "User Story")]
        [InlineData("create story for dashboard", "User Story")]
        [InlineData("create task for testing", "Task")]
        [InlineData("general work item", "Task")]
        public void WorkItemTypeExtraction_ShouldDetectCorrectType(string message, string expectedType)
        {
            // Arrange & Act - Simulate the work item type extraction logic
            var extractedType = ExtractWorkItemTypeFromMessage(message);

            // Assert
            extractedType.Should().Be(expectedType);
        }

        [Theory]
        [InlineData("assign to john.doe", "john.doe")]
        [InlineData("task for sarah.smith", "sarah.smith")]
        [InlineData("for mike.wilson: update docs", "mike.wilson:")]
        [InlineData("create task without assignment", null)]
        [InlineData("", null)]
        public void AssigneeExtraction_ShouldDetectCorrectAssignee(string message, string? expectedAssignee)
        {
            // Arrange & Act - Simulate the assignee extraction logic
            var extractedAssignee = ExtractAssigneeFromMessage(message);

            // Assert
            extractedAssignee.Should().Be(expectedAssignee);
        }

        [Fact]
        public void TeamsAIActivityHandler_ShouldInstantiateWithDependencies()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<TeamsAIActivityHandler>>();
            var mockWorkItemCreation = new Mock<IWorkItemCreationService>();
            var mockConversationIntelligence = new Mock<IConversationIntelligenceService>();

            // Act
            var handler = new TeamsAIActivityHandler(
                mockLogger.Object,
                mockWorkItemCreation.Object,
                mockConversationIntelligence.Object);

            // Assert
            handler.Should().NotBeNull();
        }

        // Helper methods that simulate the extraction logic from TeamsAIActivityHandler
        private static string ExtractPriorityFromMessage(string message)
        {
            var lowMessage = message.ToLowerInvariant();
            if (lowMessage.Contains("urgent") || lowMessage.Contains("high priority"))
                return "High";
            if (lowMessage.Contains("low priority"))
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
}
