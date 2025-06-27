namespace McpServer.Models;

/// <summary>
/// Exception thrown by MCP tools when parameter validation or execution fails
/// </summary>
public class McpToolException : Exception
{
    public McpToolException(string message) : base(message) { }
    public McpToolException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Request model for creating Azure DevOps work items
/// </summary>
public record WorkItemRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string WorkItemType { get; init; }
    public string? Priority { get; init; }
    public string? AssignedTo { get; init; }
}

/// <summary>
/// Result model for Azure DevOps work item operations
/// </summary>
public record WorkItemResult
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string WorkItemType { get; init; }
    public required string State { get; init; }
    public string? Priority { get; init; }
    public string? AssignedTo { get; init; }
}
