using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using McpServer.Models;
using McpServer.Services;
using Moq;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.IO;

namespace McpServer.Tests.Integration;

public class CreateWorkItemIntegrationTests : IClassFixture<CreateWorkItemIntegrationTests.CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CreateWorkItemIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Ensure the content root is the McpServer project directory so deps file is found
            var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "McpServer"));
            builder.UseContentRoot(projectDir);
            builder.ConfigureServices(services =>
            {
                // Remove the real Azure DevOps service
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAzureDevOpsService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add a mock Azure DevOps service for integration tests
                var mockAdoService = new Mock<IAzureDevOpsService>();
                
                mockAdoService.Setup(x => x.CreateWorkItemAsync(It.IsAny<WorkItemRequest>()))
                    .ReturnsAsync((WorkItemRequest request) => new WorkItemResult
                    {
                        Id = 12345,
                        Title = request.Title,
                        Description = request.Description,
                        WorkItemType = request.WorkItemType,
                        State = "New",
                        Priority = request.Priority,
                        AssignedTo = request.AssignedTo
                    });

                mockAdoService.Setup(x => x.GetWorkItemAsync(It.IsAny<int>()))
                    .ReturnsAsync((int id) => new WorkItemResult
                    {
                        Id = id,
                        Title = $"Mock Work Item {id}",
                        Description = "Mock description",
                        WorkItemType = "Task",
                        State = "Active"
                    });

                services.AddScoped(_ => mockAdoService.Object);
            });
        }
    }

    [Fact]
    public async Task McpController_ShouldExecuteCreateWorkItemTool_WhenValidRequestProvided()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            Id = "test-create-workitem-001",
            Method = "tools/call",
            Params = new
            {
                name = "create_work_item",
                arguments = new
                {
                    title = "Integration Test Work Item",
                    description = "Created via MCP integration test",
                    workItemType = "Task",
                    priority = "2"
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mcp", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Id.Should().NotBeNull();
        jsonResponse.Id!.ToString().Should().Be("test-create-workitem-001");
        jsonResponse.Error.Should().BeNull();
        jsonResponse.Result.Should().NotBeNull();

        // Verify the work item was created with correct properties
        var resultElement = JsonSerializer.SerializeToElement(jsonResponse.Result);
        resultElement.GetProperty("success").GetBoolean().Should().BeTrue();
        
        var dataElement = resultElement.GetProperty("data");
        dataElement.GetProperty("title").GetString().Should().Be("Integration Test Work Item");
        dataElement.GetProperty("workItemType").GetString().Should().Be("Task");
        dataElement.GetProperty("state").GetString().Should().Be("New");
        dataElement.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task McpController_ShouldReturnError_WhenCreateWorkItemToolMissingRequiredParameters()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            Id = "test-create-workitem-error-001",
            Method = "tools/call",
            Params = new
            {
                name = "create_work_item",
                arguments = new
                {
                    description = "Missing title parameter"
                    // Missing required title and workItemType
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mcp", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK); // HTTP 200, but JSON-RPC error
        
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Id.Should().NotBeNull();
        jsonResponse.Id!.ToString().Should().Be("test-create-workitem-error-001");
        jsonResponse.Error.Should().NotBeNull();
        jsonResponse.Result.Should().BeNull();
        
        jsonResponse.Error!.Message.Should().Contain("Required parameter");
    }

    [Fact]
    public async Task McpController_ShouldListCreateWorkItemTool_WhenToolsListRequested()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            Id = "test-tools-list-001",
            Method = "tools/list"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mcp", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Id.Should().NotBeNull();
        jsonResponse.Id!.ToString().Should().Be("test-tools-list-001");
        jsonResponse.Error.Should().BeNull();
        jsonResponse.Result.Should().NotBeNull();

        // Verify create_work_item tool is in the list
        var resultElement = JsonSerializer.SerializeToElement(jsonResponse.Result);
        var toolsArray = resultElement.GetProperty("tools");
        
        var createWorkItemTool = toolsArray.EnumerateArray()
            .FirstOrDefault(tool => tool.GetProperty("name").GetString() == "create_work_item");
            
        createWorkItemTool.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        createWorkItemTool.GetProperty("description").GetString().Should().Contain("work item");
        createWorkItemTool.GetProperty("description").GetString().Should().Contain("Azure DevOps");
    }
}
