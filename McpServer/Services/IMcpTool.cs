using McpServer.Models;

namespace McpServer.Services;

/// <summary>
/// Interface for MCP tools that can be executed via JSON-RPC
/// </summary>
public interface IMcpTool
{
    /// <summary>
    /// The name of the tool as exposed via MCP protocol
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human readable description of the tool
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Execute the tool with the provided parameters
    /// </summary>
    /// <param name="parameters">Tool execution parameters</param>
    /// <returns>Tool execution result</returns>
    Task<McpToolResult> ExecuteAsync(McpToolParameters parameters);
}

/// <summary>
/// Registry for managing available MCP tools
/// </summary>
public interface IMcpToolRegistry
{
    /// <summary>
    /// Register a tool in the registry
    /// </summary>
    /// <param name="tool">Tool to register</param>
    void RegisterTool(IMcpTool tool);

    /// <summary>
    /// Get a tool by name
    /// </summary>
    /// <param name="name">Tool name</param>
    /// <returns>Tool instance or null if not found</returns>
    IMcpTool? GetTool(string name);

    /// <summary>
    /// Get all registered tools
    /// </summary>
    /// <returns>Collection of all registered tools</returns>
    IEnumerable<IMcpTool> GetAllTools();
}
