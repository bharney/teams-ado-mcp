using FluentAssertions;
using McpServer.Models;
using McpServer.Services;
using Moq;
using Xunit;

namespace McpServer.Tests.Services;

/// <summary>
/// Tests for MCP tool registry and tool execution patterns
/// Following TDD approach for Phase 1.1 implementation
/// </summary>
public class McpToolRegistryTests
{
    [Fact]
    public void RegisterTool_ShouldAddToolToRegistry_WhenValidToolProvided()
    {
        // Arrange
        var registry = new McpToolRegistry();
        var mockTool = new Mock<IMcpTool>();
        mockTool.Setup(t => t.Name).Returns("test-tool");

        // Act
        registry.RegisterTool(mockTool.Object);

        // Assert
        var retrievedTool = registry.GetTool("test-tool");
        retrievedTool.Should().NotBeNull();
        retrievedTool.Should().BeSameAs(mockTool.Object);
    }

    [Fact]
    public void GetTool_ShouldReturnNull_WhenToolNotFound()
    {
        // Arrange
        var registry = new McpToolRegistry();

        // Act
        var tool = registry.GetTool("non-existent-tool");

        // Assert
        tool.Should().BeNull();
    }

    [Fact]
    public void GetAllTools_ShouldReturnEmptyCollection_WhenNoToolsRegistered()
    {
        // Arrange
        var registry = new McpToolRegistry();

        // Act
        var tools = registry.GetAllTools();

        // Assert
        tools.Should().NotBeNull();
        tools.Should().BeEmpty();
    }

    [Fact]
    public void GetAllTools_ShouldReturnAllRegisteredTools_WhenMultipleToolsRegistered()
    {
        // Arrange
        var registry = new McpToolRegistry();
        var tool1 = new Mock<IMcpTool>();
        tool1.Setup(t => t.Name).Returns("tool-1");
        var tool2 = new Mock<IMcpTool>();
        tool2.Setup(t => t.Name).Returns("tool-2");

        registry.RegisterTool(tool1.Object);
        registry.RegisterTool(tool2.Object);

        // Act
        var tools = registry.GetAllTools().ToList();

        // Assert
        tools.Should().HaveCount(2);
        tools.Should().Contain(tool1.Object);
        tools.Should().Contain(tool2.Object);
    }

    [Fact]
    public void RegisterTool_ShouldReplaceExistingTool_WhenSameNameRegistered()
    {
        // Arrange
        var registry = new McpToolRegistry();
        var originalTool = new Mock<IMcpTool>();
        originalTool.Setup(t => t.Name).Returns("duplicate-name");
        var replacementTool = new Mock<IMcpTool>();
        replacementTool.Setup(t => t.Name).Returns("duplicate-name");

        registry.RegisterTool(originalTool.Object);

        // Act
        registry.RegisterTool(replacementTool.Object);

        // Assert
        var retrievedTool = registry.GetTool("duplicate-name");
        retrievedTool.Should().BeSameAs(replacementTool.Object);
        retrievedTool.Should().NotBeSameAs(originalTool.Object);

        var allTools = registry.GetAllTools().ToList();
        allTools.Should().HaveCount(1);
        allTools.Should().Contain(replacementTool.Object);
    }
}

/// <summary>
/// Tests for MCP tool parameter handling
/// </summary>
public class McpToolParametersTests
{
    [Fact]
    public void Add_ShouldStoreParameter_WhenValidKeyValueProvided()
    {
        // Arrange
        var parameters = new McpToolParameters();

        // Act
        parameters.Add("test-key", "test-value");

        // Assert
        var value = parameters.Get<string>("test-key");
        value.Should().Be("test-value");
    }

    [Fact]
    public void Get_ShouldReturnDefault_WhenKeyNotFound()
    {
        // Arrange
        var parameters = new McpToolParameters();

        // Act
        var value = parameters.Get<string>("non-existent-key");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void Get_ShouldReturnTypedValue_WhenCorrectTypeRequested()
    {
        // Arrange
        var parameters = new McpToolParameters();
        parameters.Add("int-key", 42);
        parameters.Add("string-key", "hello");
        parameters.Add("bool-key", true);

        // Act & Assert
        parameters.Get<int>("int-key").Should().Be(42);
        parameters.Get<string>("string-key").Should().Be("hello");
        parameters.Get<bool>("bool-key").Should().BeTrue();
    }

    [Fact]
    public void Get_ShouldReturnDefault_WhenIncorrectTypeRequested()
    {
        // Arrange
        var parameters = new McpToolParameters();
        parameters.Add("string-key", "not-a-number");

        // Act
        var value = parameters.Get<int>("string-key");

        // Assert
        value.Should().Be(0); // Default for int
    }

    [Fact]
    public void TryGetValue_ShouldReturnTrue_WhenKeyExistsWithCorrectType()
    {
        // Arrange
        var parameters = new McpToolParameters();
        parameters.Add("test-key", "test-value");

        // Act
        var result = parameters.TryGetValue<string>("test-key", out var value);

        // Assert
        result.Should().BeTrue();
        value.Should().Be("test-value");
    }

    [Fact]
    public void TryGetValue_ShouldReturnFalse_WhenKeyNotFound()
    {
        // Arrange
        var parameters = new McpToolParameters();

        // Act
        var result = parameters.TryGetValue<string>("non-existent-key", out var value);

        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void ToDictionary_ShouldReturnCopyOfParameters()
    {
        // Arrange
        var parameters = new McpToolParameters();
        parameters.Add("key1", "value1");
        parameters.Add("key2", 42);

        // Act
        var dictionary = parameters.ToDictionary();

        // Assert
        dictionary.Should().HaveCount(2);
        dictionary["key1"].Should().Be("value1");
        dictionary["key2"].Should().Be(42);
    }
}

/// <summary>
/// Tests for MCP tool result handling
/// </summary>
public class McpToolResultTests
{
    [Fact]
    public void Successful_ShouldCreateSuccessfulResult_WithData()
    {
        // Arrange & Act
        var result = McpToolResult.Successful("test-data");

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be("test-data");
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Successful_ShouldCreateSuccessfulResult_WithoutData()
    {
        // Arrange & Act
        var result = McpToolResult.Successful();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Failed_ShouldCreateFailedResult_WithCustomErrorCode()
    {
        // Arrange & Act
        var result = McpToolResult.Failed("Test error", 500);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Test error");
        result.ErrorCode.Should().Be(500);
        result.Data.Should().BeNull();
    }

    [Fact]
    public void Failed_ShouldCreateFailedResult_WithDefaultErrorCode()
    {
        // Arrange & Act
        var result = McpToolResult.Failed("Test error");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Test error");
        result.ErrorCode.Should().Be(-1);
        result.Data.Should().BeNull();
    }
}
