using McpServer.Models;

namespace McpServer.Services;

/// <summary>
/// Registry for managing available MCP tools with thread-safe operations
/// </summary>
public class McpToolRegistry : IMcpToolRegistry
{
    private readonly Dictionary<string, IMcpTool> _tools = new();
    private readonly object _lock = new();

    /// <summary>
    /// Register a tool in the registry
    /// </summary>
    /// <param name="tool">Tool to register</param>
    public void RegisterTool(IMcpTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        
        lock (_lock)
        {
            _tools[tool.Name] = tool;
        }
    }

    /// <summary>
    /// Get a tool by name
    /// </summary>
    /// <param name="name">Tool name</param>
    /// <returns>Tool instance or null if not found</returns>
    public IMcpTool? GetTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            return _tools.TryGetValue(name, out var tool) ? tool : null;
        }
    }

    /// <summary>
    /// Get all registered tools
    /// </summary>
    /// <returns>Collection of all registered tools</returns>
    public IEnumerable<IMcpTool> GetAllTools()
    {
        lock (_lock)
        {
            return _tools.Values.ToList(); // Return a copy to avoid thread safety issues
        }
    }
}
