using McpServer.Models;
using McpServer.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace McpServer.Controllers;

/// <summary>
/// MCP JSON-RPC 2.0 endpoint controller
/// Handles tool execution requests following MCP protocol specification
/// </summary>
[ApiController]
[Route("api/mcp")]
public class McpController : ControllerBase
{
    private readonly IMcpToolRegistry _toolRegistry;
    private readonly ILogger<McpController> _logger;

    public McpController(IMcpToolRegistry toolRegistry, ILogger<McpController> logger)
    {
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Handle JSON-RPC 2.0 requests for MCP tool execution
    /// </summary>
    /// <param name="request">JSON-RPC request</param>
    /// <returns>JSON-RPC response</returns>
    [HttpPost]
    public async Task<IActionResult> HandleRequest([FromBody] JsonRpcRequest request)
    {
        try
        {
            // Validate JSON-RPC version
            if (request.JsonRpc != "2.0")
            {
                var errorResponse = new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = -32600,
                        Message = "Invalid JSON-RPC version. Expected '2.0'."
                    },
                    Id = request.Id
                };
                return Ok(errorResponse);
            }

            // Handle notification requests (no id field)
            if (request.Id == null)
            {
                await ProcessNotificationAsync(request);
                return Ok(); // No response for notifications
            }

            // Handle method requests
            var response = await ProcessMethodRequestAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MCP request");
            
            var errorResponse = new JsonRpcResponse
            {
                Error = new JsonRpcError
                {
                    Code = -32603,
                    Message = "Internal error"
                },
                Id = request.Id
            };
            return Ok(errorResponse);
        }
    }

    private async Task ProcessNotificationAsync(JsonRpcRequest request)
    {
        _logger.LogInformation("Processing notification: {Method}", request.Method);
        
        // For now, just log notifications
        // In a real implementation, this might trigger background processing
        await Task.CompletedTask;
    }

    private async Task<JsonRpcResponse> ProcessMethodRequestAsync(JsonRpcRequest request)
    {
        switch (request.Method)
        {
            case "tools/list":
                return await HandleToolsListAsync(request);
            
            case "tools/call":
                return await HandleToolCallAsync(request);
            
            default:
                return new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = -32601,
                        Message = "Method not found"
                    },
                    Id = request.Id
                };
        }
    }

    private async Task<JsonRpcResponse> HandleToolsListAsync(JsonRpcRequest request)
    {
        var tools = _toolRegistry.GetAllTools();
        var toolList = tools.Select(tool => new
        {
            name = tool.Name,
            // Add other tool metadata as needed
        }).ToArray();

        return new JsonRpcResponse
        {
            Result = toolList,
            Id = request.Id
        };
    }

    private async Task<JsonRpcResponse> HandleToolCallAsync(JsonRpcRequest request)
    {
        // Extract tool call parameters
        if (request.Params == null)
        {
            return new JsonRpcResponse
            {
                Error = new JsonRpcError
                {
                    Code = -32602,
                    Message = "Invalid params: tool call requires parameters"
                },
                Id = request.Id
            };
        }

        try
        {
            // Parse tool call parameters
            var paramsJson = JsonSerializer.Serialize(request.Params);
            var toolCall = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsJson);
            
            if (toolCall == null || !toolCall.TryGetValue("name", out var toolNameObj))
            {
                return new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = -32602,
                        Message = "Invalid params: missing tool name"
                    },
                    Id = request.Id
                };
            }

            var toolName = toolNameObj.ToString();
            var tool = _toolRegistry.GetTool(toolName);
            
            if (tool == null)
            {
                return new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = -32601,
                        Message = $"Tool '{toolName}' not found"
                    },
                    Id = request.Id
                };
            }

            // Extract and convert parameters
            var parameters = new McpToolParameters();
            if (toolCall.TryGetValue("arguments", out var argumentsObj))
            {
                var argumentsJson = JsonSerializer.Serialize(argumentsObj);
                var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson);
                
                if (arguments != null)
                {
                    foreach (var kvp in arguments)
                    {
                        parameters.Add(kvp.Key, kvp.Value);
                    }
                }
            }

            // Execute the tool
            var result = await tool.ExecuteAsync(parameters);
            
            if (result.Success)
            {
                return new JsonRpcResponse
                {
                    Result = result.Data,
                    Id = request.Id
                };
            }
            else
            {
                return new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = result.ErrorCode ?? -1,
                        Message = result.ErrorMessage ?? "Tool execution failed"
                    },
                    Id = request.Id
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool");
            
            return new JsonRpcResponse
            {
                Error = new JsonRpcError
                {
                    Code = -32603,
                    Message = "Internal error during tool execution"
                },
                Id = request.Id
            };
        }
    }
}
