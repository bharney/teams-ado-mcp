using FluentAssertions;
using McpServer.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace McpServer.Tests;

/// <summary>
/// Tests for JSON-RPC 2.0 protocol compliance and MCP endpoint functionality
/// Following TDD approach for Phase 1.1 implementation
/// </summary>
public class JsonRpcEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public JsonRpcEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task McpEndpoint_ShouldReturnJsonRpcResponse_WhenValidRequestProvided()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            Method = "tools/list",
            Id = "test-id-001"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mcp", request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.JsonRpc.Should().Be("2.0");
        jsonResponse.Id.Should().NotBeNull();
        jsonResponse.Id.ToString().Should().Be("test-id-001");
    }

    [Fact]
    public async Task McpEndpoint_ShouldReturnError_WhenInvalidMethodProvided()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            Method = "invalid/method",
            Id = "test-id-002"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mcp", request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Error.Should().NotBeNull();
        jsonResponse.Error!.Code.Should().Be(-32601); // Method not found
        jsonResponse.Error.Message.Should().Contain("Method not found");
        jsonResponse.Id.Should().NotBeNull();
        jsonResponse.Id.ToString().Should().Be("test-id-002");
    }

    [Fact]
    public async Task McpEndpoint_ShouldReturnError_WhenInvalidJsonRpcVersion()
    {
        // Arrange
        var invalidRequest = new
        {
            jsonrpc = "1.0", // Invalid version
            method = "tools/list",
            id = "test-id-003"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mcp", invalidRequest);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Error.Should().NotBeNull();
        jsonResponse.Error!.Code.Should().Be(-32600); // Invalid Request
        jsonResponse.Error.Message.Should().Contain("Invalid JSON-RPC version");
    }

    [Fact]
    public async Task McpEndpoint_ShouldHandleNotificationRequests_WithoutId()
    {
        // Arrange - Notification request (no id field)
        var notificationRequest = new
        {
            jsonrpc = "2.0",
            method = "tools/list"
            // No id field for notifications
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mcp", notificationRequest);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        // For notifications, server should not send a response body
        var content = await response.Content.ReadAsStringAsync();
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task McpEndpoint_ShouldReturnAvailableTools_WhenToolsListRequested()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            Method = "tools/list",
            Id = "test-id-004"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mcp", request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Result.Should().NotBeNull();
        jsonResponse.Error.Should().BeNull();
        
        // Result should be an array of tool definitions
        var resultJson = JsonSerializer.Serialize(jsonResponse.Result);
        var tools = JsonSerializer.Deserialize<object[]>(resultJson);
        tools.Should().NotBeNull();
        tools!.Length.Should().BeGreaterThanOrEqualTo(0);
    }
}
