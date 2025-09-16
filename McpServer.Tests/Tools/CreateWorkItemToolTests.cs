using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using McpServer.Models;
using McpServer.Services;
using McpServer.Tools;
using Xunit;

namespace McpServer.Tests.Tools;

public class CreateWorkItemToolTests
{
    private readonly Mock<IAzureDevOpsService> _mockAdoService;
    private readonly Mock<ILogger<CreateWorkItemTool>> _mockLogger;
    private readonly CreateWorkItemTool _tool;

    public CreateWorkItemToolTests()
    {
        _mockAdoService = new Mock<IAzureDevOpsService>();
        _mockLogger = new Mock<ILogger<CreateWorkItemTool>>();
        _tool = new CreateWorkItemTool(_mockAdoService.Object, _mockLogger.Object);
    }

    [Fact]
    public void Name_ShouldReturnCreateWorkItem()
    {
        // Act
        var name = _tool.Name;

        // Assert
        name.Should().Be("create_work_item");
    }

    [Fact]
    public void Description_ShouldProvideToolDescription()
    {
        // Act
        var description = _tool.Description;

        // Assert
        description.Should().NotBeNullOrEmpty();
        description.Should().Contain("work item");
        description.Should().Contain("Azure DevOps");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateWorkItem_WhenValidParametersProvided()
    {
        // Arrange
        var expectedWorkItem = new WorkItemResult
        {
            Id = 123,
            Title = "Test Work Item",
            State = "New",
            WorkItemType = "Task"
        };

        _mockAdoService
            .Setup(x => x.CreateWorkItemAsync(It.IsAny<WorkItemRequest>()))
            .ReturnsAsync(expectedWorkItem);

        var parameters = new McpToolParameters();
        parameters.Add("title", "Test Work Item");
        parameters.Add("description", "Test description");
        parameters.Add("workItemType", "Task");

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();

        var workItem = JsonSerializer.Deserialize<WorkItemResult>(JsonSerializer.Serialize(result.Data));
        workItem.Should().NotBeNull();
        workItem!.Id.Should().Be(123);
        workItem.Title.Should().Be("Test Work Item");

        _mockAdoService.Verify(x => x.CreateWorkItemAsync(It.Is<WorkItemRequest>(req =>
            req.Title == "Test Work Item" &&
            req.Description == "Test description" &&
            req.WorkItemType == "Task"
        )), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenTitleParameterMissing()
    {
        // Arrange
        var parameters = new McpToolParameters();
        parameters.Add("description", "Test description");
        parameters.Add("workItemType", "Task");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<McpToolException>(() => _tool.ExecuteAsync(parameters));
        exception.Message.Should().Contain("title");
        exception.Message.Should().Contain("Required");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenWorkItemTypeParameterMissing()
    {
        // Arrange
        var parameters = new McpToolParameters();
        parameters.Add("title", "Test Work Item");
        parameters.Add("description", "Test description");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<McpToolException>(() => _tool.ExecuteAsync(parameters));
        exception.Message.Should().Contain("workItemType");
        exception.Message.Should().Contain("Required");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseDefaultValues_WhenOptionalParametersOmitted()
    {
        // Arrange
        var expectedWorkItem = new WorkItemResult
        {
            Id = 456,
            Title = "Test Task",
            State = "New",
            WorkItemType = "Task"
        };

        _mockAdoService
            .Setup(x => x.CreateWorkItemAsync(It.IsAny<WorkItemRequest>()))
            .ReturnsAsync(expectedWorkItem);

        var parameters = new McpToolParameters();
        parameters.Add("title", "Test Task");
        parameters.Add("workItemType", "Task");
        // Omitting description and other optional parameters

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Success.Should().BeTrue();

        _mockAdoService.Verify(x => x.CreateWorkItemAsync(It.Is<WorkItemRequest>(req =>
            req.Title == "Test Task" &&
            req.WorkItemType == "Task" &&
            req.Description == null // Should be null when not provided
        )), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailureResult_WhenAdoServiceThrowsException()
    {
        // Arrange
        _mockAdoService
            .Setup(x => x.CreateWorkItemAsync(It.IsAny<WorkItemRequest>()))
            .ThrowsAsync(new InvalidOperationException("ADO service error"));

        var parameters = new McpToolParameters();
        parameters.Add("title", "Test Work Item");
        parameters.Add("workItemType", "Task");

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("ADO service error");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassAllParameters_WhenAllParametersProvided()
    {
        // Arrange
        var expectedWorkItem = new WorkItemResult
        {
            Id = 789,
            Title = "Complex Work Item",
            State = "In Progress",
            WorkItemType = "User Story"
        };

        _mockAdoService
            .Setup(x => x.CreateWorkItemAsync(It.IsAny<WorkItemRequest>()))
            .ReturnsAsync(expectedWorkItem);

        var parameters = new McpToolParameters();
        parameters.Add("title", "Complex Work Item");
        parameters.Add("description", "Detailed description with requirements");
        parameters.Add("workItemType", "User Story");
        parameters.Add("priority", "2");
        // assignedTo intentionally omitted while feature disabled

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Success.Should().BeTrue();

        _mockAdoService.Verify(x => x.CreateWorkItemAsync(It.Is<WorkItemRequest>(req =>
            req.Title == "Complex Work Item" &&
            req.Description == "Detailed description with requirements" &&
            req.WorkItemType == "User Story" &&
            req.Priority == "2"
        )), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogInformation_WhenExecutingSuccessfully()
    {
        // Arrange
        var expectedWorkItem = new WorkItemResult
        {
            Id = 999,
            Title = "Logged Work Item",
            State = "New",
            WorkItemType = "Bug"
        };

        _mockAdoService
            .Setup(x => x.CreateWorkItemAsync(It.IsAny<WorkItemRequest>()))
            .ReturnsAsync(expectedWorkItem);

        var parameters = new McpToolParameters();
        parameters.Add("title", "Logged Work Item");
        parameters.Add("workItemType", "Bug");

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Success.Should().BeTrue();

        // Verify logging occurred (checking that Info level was called)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating work item")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("Task")]
    [InlineData("Bug")]
    [InlineData("User Story")]
    [InlineData("Epic")]
    [InlineData("Feature")]
    public async Task ExecuteAsync_ShouldAcceptValidWorkItemTypes(string workItemType)
    {
        // Arrange
        var expectedWorkItem = new WorkItemResult
        {
            Id = 100,
            Title = $"Test {workItemType}",
            State = "New",
            WorkItemType = workItemType
        };

        _mockAdoService
            .Setup(x => x.CreateWorkItemAsync(It.IsAny<WorkItemRequest>()))
            .ReturnsAsync(expectedWorkItem);

        var parameters = new McpToolParameters();
        parameters.Add("title", $"Test {workItemType}");
        parameters.Add("workItemType", workItemType);

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Success.Should().BeTrue();
        _mockAdoService.Verify(x => x.CreateWorkItemAsync(It.Is<WorkItemRequest>(req =>
            req.WorkItemType == workItemType
        )), Times.Once);
    }
}
