using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using TeamsBot.Configuration;
using TeamsBot.Models;

namespace TeamsBot.Services
{
    /// <summary>
    /// Azure DevOps service following MCP patterns for API interactions
    /// Implements secure authentication and structured work item creation
    /// </summary>
    public interface IAzureDevOpsService
    {
        Task<int?> CreateWorkItemAsync(ActionItemDetails actionItem, CancellationToken cancellationToken = default);
        Task<WorkItemInfo?> GetWorkItemAsync(int workItemId, CancellationToken cancellationToken = default);
        Task<bool> UpdateWorkItemAsync(int workItemId, Dictionary<string, object> updates, CancellationToken cancellationToken = default);
    }

    public class AzureDevOpsService : IAzureDevOpsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureDevOpsService> _logger;
        private readonly ISecureConfigurationProvider _configurationProvider;
        private AzureDevOpsConfiguration? _configuration;

        public AzureDevOpsService(
            HttpClient httpClient,
            ILogger<AzureDevOpsService> logger,
            ISecureConfigurationProvider configurationProvider)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        }

        private async Task EnsureConfigurationAsync(CancellationToken cancellationToken)
        {
            if (_configuration == null)
            {
                var config = await _configurationProvider.GetConfigurationAsync(cancellationToken);
                _configuration = config.AzureDevOps;

                // Configure HTTP client authentication
                if (!string.IsNullOrEmpty(_configuration.PersonalAccessToken))
                {
                    var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_configuration.PersonalAccessToken}"));
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                }

                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "TeamsBot/1.0");
            }
        }

        public async Task<int?> CreateWorkItemAsync(ActionItemDetails actionItem, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureConfigurationAsync(cancellationToken);

                if (_configuration == null || string.IsNullOrEmpty(_configuration.Organization) || string.IsNullOrEmpty(_configuration.Project))
                {
                    _logger.LogError("Azure DevOps configuration is incomplete");
                    return null;
                }

                _logger.LogInformation("Creating work item: {Title}", actionItem.Title);

                // Prepare work item data using ADO REST API format
                var workItemData = new List<object>
                {
                    new { op = "add", path = "/fields/System.Title", value = actionItem.Title },
                    new { op = "add", path = "/fields/System.Description", value = actionItem.Description },
                    new { op = "add", path = "/fields/Microsoft.VSTS.Common.Priority", value = GetPriorityValue(actionItem.Priority) },
                    new { op = "add", path = "/fields/System.WorkItemType", value = actionItem.WorkItemType }
                };

                // Add assignee if specified
                if (!string.IsNullOrEmpty(actionItem.AssignedTo))
                {
                    workItemData.Add(new { op = "add", path = "/fields/System.AssignedTo", value = actionItem.AssignedTo });
                }

                // Add default area and iteration paths if configured
                if (!string.IsNullOrEmpty(_configuration.DefaultAreaPath))
                {
                    workItemData.Add(new { op = "add", path = "/fields/System.AreaPath", value = _configuration.DefaultAreaPath });
                }

                if (!string.IsNullOrEmpty(_configuration.DefaultIterationPath))
                {
                    workItemData.Add(new { op = "add", path = "/fields/System.IterationPath", value = _configuration.DefaultIterationPath });
                }

                var json = JsonSerializer.Serialize(workItemData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");

                // Create work item via ADO REST API
                var url = $"https://dev.azure.com/{_configuration.Organization}/{_configuration.Project}/_apis/wit/workitems/${actionItem.WorkItemType}?api-version=7.0";
                var response = await _httpClient.PostAsync(url, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var workItem = JsonSerializer.Deserialize<WorkItemResponse>(responseContent);
                    
                    _logger.LogInformation("Successfully created work item {WorkItemId}", workItem?.Id);
                    return workItem?.Id;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to create work item. Status: {StatusCode}, Error: {Error}", 
                        response.StatusCode, errorContent);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating work item in Azure DevOps");
                return null;
            }
        }

        public async Task<WorkItemInfo?> GetWorkItemAsync(int workItemId, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureConfigurationAsync(cancellationToken);

                if (_configuration == null || string.IsNullOrEmpty(_configuration.Organization))
                {
                    _logger.LogError("Azure DevOps configuration is incomplete");
                    return null;
                }

                var url = $"https://dev.azure.com/{_configuration.Organization}/_apis/wit/workitems/{workItemId}?api-version=7.0";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var workItem = JsonSerializer.Deserialize<WorkItemResponse>(responseContent);
                    
                    return workItem != null ? new WorkItemInfo
                    {
                        Id = workItem.Id,
                        Title = workItem.Fields?.Title ?? string.Empty,
                        State = workItem.Fields?.State ?? string.Empty,
                        AssignedTo = workItem.Fields?.AssignedTo ?? string.Empty,
                        Url = workItem.Url ?? string.Empty
                    } : null;
                }
                else
                {
                    _logger.LogError("Failed to get work item {WorkItemId}. Status: {StatusCode}", 
                        workItemId, response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting work item {WorkItemId} from Azure DevOps", workItemId);
                return null;
            }
        }

        public async Task<bool> UpdateWorkItemAsync(int workItemId, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureConfigurationAsync(cancellationToken);

                if (_configuration == null || string.IsNullOrEmpty(_configuration.Organization))
                {
                    _logger.LogError("Azure DevOps configuration is incomplete");
                    return false;
                }

                var patchData = updates.Select(kvp => new { op = "replace", path = $"/fields/{kvp.Key}", value = kvp.Value });
                var json = JsonSerializer.Serialize(patchData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");

                var url = $"https://dev.azure.com/{_configuration.Organization}/_apis/wit/workitems/{workItemId}?api-version=7.0";
                var response = await _httpClient.PatchAsync(url, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully updated work item {WorkItemId}", workItemId);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to update work item {WorkItemId}. Status: {StatusCode}, Error: {Error}", 
                        workItemId, response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating work item {WorkItemId} in Azure DevOps", workItemId);
                return false;
            }
        }

        private static int GetPriorityValue(string priority)
        {
            return priority.ToLowerInvariant() switch
            {
                "high" => 1,
                "medium" => 2,
                "low" => 3,
                _ => 2
            };
        }
    }

    /// <summary>
    /// Azure DevOps work item response model
    /// </summary>
    public class WorkItemResponse
    {
        public int Id { get; set; }
        public string? Url { get; set; }
        public WorkItemFields? Fields { get; set; }
    }

    public class WorkItemFields
    {
        public string? Title { get; set; }
        public string? State { get; set; }
        public string? AssignedTo { get; set; }
    }

    /// <summary>
    /// Simplified work item information for external consumption
    /// </summary>
    public class WorkItemInfo
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
