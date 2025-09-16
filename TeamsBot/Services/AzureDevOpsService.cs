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

                if (string.IsNullOrWhiteSpace(_configuration.PersonalAccessToken))
                {
                    _logger.LogWarning("Skipping work item creation: Azure DevOps PAT not loaded. Ensure user secret 'AzureDevOpsPersonalAccessToken' or 'AzureDevOps:PersonalAccessToken' is set for TeamsBot project.");
                    return null;
                }

                _logger.LogInformation("Creating work item: {Title}", actionItem.Title);

                // Prepare work item data using ADO REST API format
                var workItemData = new List<object>
                {
                    new { op = "add", path = "/fields/System.Title", value = actionItem.Title },
                    new { op = "add", path = "/fields/System.Description", value = actionItem.Description },
                    new { op = "add", path = "/fields/Microsoft.VSTS.Common.Priority", value = GetPriorityValue(actionItem.Priority) },
                    // NOTE: Do NOT patch System.WorkItemType when creating; the type is specified in the URL ($Task, $Bug, etc.)
                };

                // Add assignee if specified
                if (!string.IsNullOrEmpty(actionItem.AssignedTo))
                {
                    if (IsLikelyIdentity(actionItem.AssignedTo))
                    {
                        workItemData.Add(new { op = "add", path = "/fields/System.AssignedTo", value = actionItem.AssignedTo });
                    }
                    else
                    {
                        _logger.LogInformation("Skipping AssignedTo value '{AssignedTo}' – doesn't look like a resolvable identity (expect email/UPN).", actionItem.AssignedTo);
                    }
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
                _logger.LogDebug("Work item PATCH payload: {Payload}", json);
                var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");

                // Create work item via ADO REST API (type specified via $Type suffix)
                var url = $"https://dev.azure.com/{_configuration.Organization}/{_configuration.Project}/_apis/wit/workitems/${actionItem.WorkItemType}?api-version=7.0";
                _logger.LogDebug("POST {Url}", url);

                var response = await _httpClient.PostAsync(url, content, cancellationToken);

                var contentType = response.Content.Headers.ContentType?.MediaType;
                var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // Attempt targeted diagnostics
                    string diagnostic = string.Empty;
                    try
                    {
                        using var doc = JsonDocument.Parse(rawBody);
                        var root = doc.RootElement;
                        var message = TryGetJsonString(root, "message") ?? string.Empty;
                        var typeKey = TryGetJsonString(root, "typeKey") ?? string.Empty;
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && message.Contains("expired", StringComparison.OrdinalIgnoreCase))
                        {
                            diagnostic = "Personal Access Token appears expired. Generate a new PAT (Work Items Read & Write) and update user-secrets: dotnet user-secrets set AzureDevOpsPersonalAccessToken <NEW_PAT> --project ./TeamsBot";
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && typeKey.Equals("WorkItemFieldInvalidException", StringComparison.OrdinalIgnoreCase) && message.Contains("Assigned To", StringComparison.OrdinalIgnoreCase))
                        {
                            diagnostic = "AssignedTo value not recognized. Supply a valid email/UPN or omit it. The bot skipped heuristic filtering but server still rejected value.";
                        }
                    }
                    catch { /* ignore parse issues */ }

                    _logger.LogError("Failed to create work item. Status: {StatusCode}, ContentType: {ContentType}, Body (truncated 500): {Body}{Diag}",
                        response.StatusCode,
                        contentType,
                        rawBody.Length > 500 ? rawBody.Substring(0, 500) + "..." : rawBody,
                        string.IsNullOrEmpty(diagnostic) ? string.Empty : $" Guidance: {diagnostic}");
                    return null;
                }

                if (contentType is not null && !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Unexpected non-JSON response creating work item. ContentType: {ContentType}, First200: {Snippet}",
                        contentType,
                        rawBody.Length > 200 ? rawBody.Substring(0, 200) + "..." : rawBody);
                    return null;
                }

                try
                {
                    var workItem = JsonSerializer.Deserialize<WorkItemResponse>(rawBody);
                    if (workItem?.Id != null)
                    {
                        _logger.LogInformation("Successfully created work item {WorkItemId}", workItem.Id);
                        return workItem.Id;
                    }
                    _logger.LogWarning("JSON parsed but work item id missing. Raw (first 300): {Snippet}", rawBody.Length > 300 ? rawBody.Substring(0, 300) + "..." : rawBody);
                    return null;
                }
                catch (JsonException jex)
                {
                    _logger.LogError(jex, "JSON deserialization failed for work item create response. First 300 chars: {Snippet}", rawBody.Length > 300 ? rawBody.Substring(0, 300) + "..." : rawBody);
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

        private static bool IsLikelyIdentity(string value)
        {
            // Simple heuristic: email / UPN or GUID
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (value.Contains('@')) return true;
            if (Guid.TryParse(value, out _)) return true;
            // Could extend with ADO identity format checks later
            return false;
        }

        private static string? TryGetJsonString(JsonElement root, string propertyName)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String)
                    return prop.GetString();
                return prop.ToString();
            }
            return null;
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
