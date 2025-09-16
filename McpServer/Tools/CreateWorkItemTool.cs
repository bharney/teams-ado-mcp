using Microsoft.Extensions.Logging;
using McpServer.Models;
using McpServer.Services;

namespace McpServer.Tools;

/// <summary>
/// MCP tool for creating Azure DevOps work items
/// </summary>
public class CreateWorkItemTool : IMcpTool
{
    private readonly IAzureDevOpsService _adoService;
    private readonly ILogger<CreateWorkItemTool> _logger;

    public CreateWorkItemTool(IAzureDevOpsService adoService, ILogger<CreateWorkItemTool> logger)
    {
        _adoService = adoService ?? throw new ArgumentNullException(nameof(adoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "create_work_item";

    public string Description => "Creates a new work item in Azure DevOps with the specified title, description, and work item type.";

    public async Task<McpToolResult> ExecuteAsync(McpToolParameters parameters)
    {
        try
        {
            // Validate required parameters
            var title = parameters.GetValue<string>("title", required: true);
            var workItemType = parameters.GetValue<string>("workItemType", required: true);

            // Get optional parameters
            var description = parameters.GetValue<string>("description", required: false);
            var priority = parameters.GetValue<string>("priority", required: false);
            // AssignedTo parameter intentionally ignored (feature temporarily disabled)
            // var assignedTo = parameters.GetValue<string>("assignedTo", required: false);

            _logger.LogInformation("Creating work item: {Title} of type {WorkItemType}", title, workItemType);

            // Create work item request
            var workItemRequest = new WorkItemRequest
            {
                Title = title,
                Description = description,
                WorkItemType = workItemType,
                Priority = priority
            };

            // Call Azure DevOps service
            var workItem = await _adoService.CreateWorkItemAsync(workItemRequest);

            _logger.LogInformation("Successfully created work item with ID: {WorkItemId}", workItem.Id);

            return new McpToolResult 
            { 
                Success = true, 
                Data = workItem 
            };
        }
        catch (McpToolException)
        {
            // Re-throw MCP tool exceptions (parameter validation, etc.)
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work item");
            
            return new McpToolResult
            {
                Success = false,
                ErrorMessage = $"Failed to create work item: {ex.Message}"
            };
        }
    }
}
