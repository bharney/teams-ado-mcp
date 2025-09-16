using Microsoft.Extensions.Logging;
using Moq;
using TeamsBot.Services;
using TeamsBot.Models;
using McpServer.Models; // for WorkItemResult after consolidation
using Xunit;
using FluentAssertions;

namespace TeamsBot.Tests.Services
{
    /// <summary>
    /// Tests for MCP-related services following Azure MCP patterns
    /// Fast, isolated unit tests for conversation management and action item extraction
    /// </summary>
    public class McpServicesTests
    {
        private readonly Mock<ILogger<ActionItemExtractor>> _mockActionLogger;
        private readonly Mock<ILogger<ConversationService>> _mockConversationLogger;
        private readonly ActionItemExtractor _actionItemExtractor;
        private readonly ConversationService _conversationService;

        public McpServicesTests()
        {
            _mockActionLogger = new Mock<ILogger<ActionItemExtractor>>();
            _mockConversationLogger = new Mock<ILogger<ConversationService>>();
            _actionItemExtractor = new ActionItemExtractor(_mockActionLogger.Object);
            _conversationService = new ConversationService(_mockConversationLogger.Object);
        }

        [Fact]
        public void ActionItemExtractor_Constructor_ShouldNotThrow()
        {
            // Act & Assert
            var action = () => new ActionItemExtractor(_mockActionLogger.Object);
            action.Should().NotThrow();
        }

        [Fact]
        public void ConversationService_Constructor_ShouldNotThrow()
        {
            // Act & Assert
            var action = () => new ConversationService(_mockConversationLogger.Object);
            action.Should().NotThrow();
        }

        [Theory]
        [InlineData("create bug for login issue", WorkItemType.Bug)]
        [InlineData("implement new feature", WorkItemType.Task)]
        [InlineData("user story for dashboard", WorkItemType.UserStory)]
        [InlineData("epic for mobile app", WorkItemType.Epic)]
        [InlineData("regular task", WorkItemType.Task)]
        public async Task ClassifyWorkItemTypeAsync_WithVariousInputs_ShouldReturnCorrectType(string input, WorkItemType expected)
        {
            // Act
            var result = await _actionItemExtractor.ClassifyWorkItemTypeAsync(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("create urgent task", "High")]
        [InlineData("low priority fix", "Low")]
        [InlineData("normal task", "Medium")]
        public async Task ExtractActionItemAsync_WithPriorityKeywords_ShouldSetCorrectPriority(string input, string expectedPriority)
        {
            // Act
            var result = await _actionItemExtractor.ExtractActionItemAsync(input);

            // Assert
            result.Should().NotBeNull();
            result!.Priority.Should().Be(expectedPriority);
        }

        [Fact]
        public async Task ExtractActionItemAsync_WithEmptyString_ShouldReturnNull()
        {
            // Act
            var result = await _actionItemExtractor.ExtractActionItemAsync("");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task ExtractActionItemAsync_WithNullString_ShouldReturnNull()
        {
            // Act
            var result = await _actionItemExtractor.ExtractActionItemAsync(null!);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void TodoItem_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var todoItem = new TodoItem();

            // Assert
            todoItem.Title.Should().BeEmpty();
            todoItem.Description.Should().BeEmpty();
            todoItem.WorkItemType.Should().Be(WorkItemType.Task);
            todoItem.Priority.Should().Be("Medium");
            todoItem.Assignee.Should().BeNull();
            todoItem.AdditionalProperties.Should().NotBeNull();
            todoItem.AdditionalProperties.Should().BeEmpty();
        }

        [Fact]
        public void MeetingContext_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var context = new MeetingContext();

            // Assert
            context.ConversationId.Should().BeEmpty();
            context.Message.Should().BeEmpty();
            context.FacilitatorId.Should().BeNull();
            context.Participants.Should().NotBeNull();
            context.Participants.Should().BeEmpty();
            context.Metadata.Should().NotBeNull();
            context.Metadata.Should().BeEmpty();
        }

        // WorkItemResult tests now covered in McpServer test project; removed duplicate default test.

        [Theory]
        [InlineData(WorkItemType.Bug, "Bug")]
        [InlineData(WorkItemType.Task, "Task")]
        [InlineData(WorkItemType.UserStory, "UserStory")]
        [InlineData(WorkItemType.Epic, "Epic")]
        public void WorkItemType_EnumValues_ShouldHaveCorrectNames(WorkItemType workItemType, string expectedName)
        {
            // Act & Assert
            workItemType.ToString().Should().Be(expectedName);
        }

        [Fact]
        public async Task ConversationService_GetMeetingContextAsync_ShouldReturnEmptyContext()
        {
            // Act
            var result = await _conversationService.GetMeetingContextAsync("test-conversation", CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.ConversationId.Should().Be("test-conversation");
        }

        [Fact]
        public async Task ConversationService_GetRecentMessagesAsync_ShouldReturnEmptyList()
        {
            // Act
            var result = await _conversationService.GetRecentMessagesAsync("test-conversation", 10, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
    }
}
