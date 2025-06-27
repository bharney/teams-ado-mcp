using McpServer.Models;

namespace McpServer.Services;

/// <summary>
/// Interface for Azure DevOps operations within MCP Server
/// </summary>
public interface IAzureDevOpsService
{
    Task<WorkItemResult> CreateWorkItemAsync(WorkItemRequest request);
    Task<WorkItemResult> GetWorkItemAsync(int id);
    Task<IEnumerable<WorkItemResult>> GetWorkItemsAsync(string? query = null);
    Task<WorkItemResult> UpdateWorkItemAsync(int id, WorkItemRequest request);
}
