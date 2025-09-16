using McpServer.Models;
using McpServer.Services;
using TeamsBot.Models;

namespace TeamsBot.Services;

public interface IWorkItemCreationService
{
  Task<WorkItemResult?> CreateFromActionItemAsync(ActionItemDetails actionItem, CancellationToken ct = default);
}

public class WorkItemCreationService : IWorkItemCreationService
{
  private readonly McpServer.Services.IAzureDevOpsService _ado;

  public WorkItemCreationService(McpServer.Services.IAzureDevOpsService ado) => _ado = ado;

  public async Task<WorkItemResult?> CreateFromActionItemAsync(ActionItemDetails actionItem, CancellationToken ct = default)
  {
    if (actionItem == null) throw new ArgumentNullException(nameof(actionItem));
    if (string.IsNullOrWhiteSpace(actionItem.Title)) return null;

    var request = new WorkItemRequest
    {
      Title = actionItem.Title,
      Description = actionItem.Description,
      Priority = actionItem.Priority,
      AssignedTo = actionItem.AssignedTo,
      WorkItemType = string.IsNullOrWhiteSpace(actionItem.WorkItemType) ? "Task" : actionItem.WorkItemType
    };

    return await _ado.CreateWorkItemAsync(request);
  }
}
