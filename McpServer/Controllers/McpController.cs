using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using McpServer.Models;
using McpServer.Services;

namespace McpServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class McpController : ControllerBase
{
    private readonly ILogger<McpController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMcpToolRegistry _toolRegistry;

    public McpController(ILogger<McpController> logger, IConfiguration configuration, IMcpToolRegistry toolRegistry)
    {
        _logger = logger;
        _configuration = configuration;
        _toolRegistry = toolRegistry;
    }

    [HttpGet("info")]
    public IActionResult GetInfo()
    {
        var mcpConfig = _configuration.GetSection("Mcp");
        var info = new
        {
            ServerName = mcpConfig["ServerName"] ?? "teams-ado-mcp-server",
            Version = mcpConfig["Version"] ?? "1.0.0",
            Description = mcpConfig["Description"] ?? "MCP server for Teams-Azure DevOps integration",
            Capabilities = new[] { "create-work-item", "list-work-items", "update-work-item" },
            Status = "running",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        };

        return Ok(info);
    }

    /// <summary>
    /// JSON-RPC endpoint implementing basic MCP methods: tools/list, tools/call
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ExecuteJsonRpc([FromBody] JsonRpcRequest request)
    {
        if (request == null)
        {
            return Ok(new JsonRpcResponse
            {
                Id = null,
                Error = new JsonRpcError { Code = -32600, Message = "Invalid request" }
            });
        }

        // Validate JSON-RPC version
        if (!string.Equals(request.JsonRpc, "2.0", StringComparison.Ordinal))
        {
            return Ok(new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError { Code = -32600, Message = "Invalid JSON-RPC version" }
            });
        }

        // Notification (no id) handling: execute but return empty body per JSON-RPC 2.0
        bool isNotification = request.Id is null;

        try
        {
            switch (request.Method)
            {
                case "tools/list":
                    var tools = _toolRegistry.GetAllTools().Select(t => new
                    {
                        name = t.Name,
                        description = t.Description
                    });
                    if (isNotification)
                    {
                        // Execute silently for notification
                        return NoContent();
                    }
                    return Ok(new JsonRpcResponse { Id = request.Id, Result = new { tools } });

                case "tools/call":
                    if (request.Params is null)
                    {
                        if (isNotification) return NoContent();
                        return Ok(new JsonRpcResponse { Id = request.Id, Error = new JsonRpcError { Code = -32602, Message = "Missing params" } });
                    }

                    // Expecting { name: string, arguments: { ... } }
                    var rootElement = JsonSerializer.SerializeToElement(request.Params);
                    if (!rootElement.TryGetProperty("name", out var nameElement))
                    {
                        if (isNotification) return NoContent();
                        return Ok(new JsonRpcResponse { Id = request.Id, Error = new JsonRpcError { Code = -32602, Message = "Tool name not provided" } });
                    }
                    var toolName = nameElement.GetString();
                    var tool = toolName is null ? null : _toolRegistry.GetTool(toolName);
                    if (tool == null)
                    {
                        if (isNotification) return NoContent();
                        return Ok(new JsonRpcResponse { Id = request.Id, Error = new JsonRpcError { Code = -32601, Message = $"Tool '{toolName}' not found" } });
                    }

                    var parameters = new McpToolParameters();
                    if (rootElement.TryGetProperty("arguments", out var argsElement) && argsElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in argsElement.EnumerateObject())
                        {
                            parameters.Add(prop.Name, prop.Value);
                        }
                    }

                    try
                    {
                        var result = await tool.ExecuteAsync(parameters);
                        if (isNotification) return NoContent();
                        if (result.Success)
                            return Ok(new JsonRpcResponse { Id = request.Id, Result = new { success = true, data = result.Data } });
                        return Ok(new JsonRpcResponse { Id = request.Id, Result = new { success = false, error = result.ErrorMessage, code = result.ErrorCode } });
                    }
                    catch (McpToolException ex)
                    {
                        if (isNotification) return NoContent();
                        return Ok(new JsonRpcResponse { Id = request.Id, Error = new JsonRpcError { Code = -32602, Message = ex.Message } });
                    }

                default:
                    if (isNotification) return NoContent();
                    return Ok(new JsonRpcResponse { Id = request.Id, Error = new JsonRpcError { Code = -32601, Message = "Method not found" } });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing JSON-RPC request {Method}", request.Method);
            if (isNotification) return NoContent();
            return Ok(new JsonRpcResponse { Id = request.Id, Error = new JsonRpcError { Code = -32603, Message = "Internal error", Data = ex.Message } });
        }
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    [HttpPost("test-secrets")]
    public IActionResult TestSecrets()
    {
        try
        {
            var adoConfig = _configuration.GetSection("AzureDevOps");
            var hasPatConfigured = !string.IsNullOrEmpty(adoConfig["PersonalAccessToken"]);
            var hasOrgConfigured = !string.IsNullOrEmpty(adoConfig["Organization"]);
            var hasProjectConfigured = !string.IsNullOrEmpty(adoConfig["Project"]);

            return Ok(new
            {
                message = "Configuration test completed",
                azureDevOps = new
                {
                    personalAccessTokenConfigured = hasPatConfigured,
                    organizationConfigured = hasOrgConfigured,
                    projectConfigured = hasProjectConfigured
                },
                configurationSource = "User Secrets (Development) / Key Vault (Production)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing configuration");
            return StatusCode(500, new { error = "Configuration test failed", message = ex.Message });
        }
    }
}
