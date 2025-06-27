using System.Text.Json;
using TeamsBot.Models;

namespace TeamsBot.Services
{
    /// <summary>
    /// Service for communicating with MCP (Model Context Protocol) server
    /// Handles Azure DevOps integration via MCP tools for work item creation
    /// </summary>
    public interface IMcpClientService
    {
        Task<WorkItemResult> CreateWorkItemAsync(TodoItem todoItem);
        Task<IEnumerable<WorkItemResult>> GetWorkItemsAsync(string? assignee = null);
        Task<bool> TestConnectionAsync();
    }

    public class McpClientService : IMcpClientService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<McpClientService> _logger;

        private readonly string _mcpServerUrl;
        private readonly string _adoOrganization;
        private readonly string _adoProject;

        public McpClientService(
            HttpClient httpClient, 
            IConfiguration configuration,
            ILogger<McpClientService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _mcpServerUrl = _configuration["McpServer:Url"] ?? "http://localhost:5000";
            _adoOrganization = _configuration["AzureDevOps:Organization"] ?? throw new InvalidOperationException("ADO Organization not configured");
            _adoProject = _configuration["AzureDevOps:Project"] ?? throw new InvalidOperationException("ADO Project not configured");
        }

        public async Task<WorkItemResult> CreateWorkItemAsync(TodoItem todoItem)
        {
            try
            {
                _logger.LogInformation($"Creating ADO work item via MCP: {todoItem.Title}");

                var mcpRequest = new McpToolRequest
                {
                    Tool = "create-work-item",
                    Parameters = new Dictionary<string, object>
                    {
                        ["title"] = todoItem.Title,
                        ["description"] = todoItem.Description,
                        ["workItemType"] = todoItem.WorkItemType.ToString(),
                        ["priority"] = todoItem.Priority,
                        ["assignee"] = todoItem.Assignee ?? "",
                        ["organization"] = _adoOrganization,
                        ["project"] = _adoProject
                    }
                };

                var jsonContent = JsonSerializer.Serialize(mcpRequest);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_mcpServerUrl}/api/mcp/tools", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var mcpResponse = JsonSerializer.Deserialize<McpToolResponse>(responseContent);

                if (mcpResponse?.Success == true && mcpResponse.Result != null)
                {
                    return JsonSerializer.Deserialize<WorkItemResult>(mcpResponse.Result.ToString()) 
                           ?? throw new InvalidOperationException("Failed to deserialize work item result");
                }

                throw new InvalidOperationException($"MCP tool execution failed: {mcpResponse?.Error}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating work item via MCP");
                throw;
            }
        }

        public async Task<IEnumerable<WorkItemResult>> GetWorkItemsAsync(string? assignee = null)
        {
            try
            {
                var mcpRequest = new McpToolRequest
                {
                    Tool = "get-work-items",
                    Parameters = new Dictionary<string, object>
                    {
                        ["organization"] = _adoOrganization,
                        ["project"] = _adoProject,
                        ["assignee"] = assignee ?? ""
                    }
                };

                var jsonContent = JsonSerializer.Serialize(mcpRequest);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_mcpServerUrl}/api/mcp/tools", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var mcpResponse = JsonSerializer.Deserialize<McpToolResponse>(responseContent);

                if (mcpResponse?.Success == true && mcpResponse.Result != null)
                {
                    return JsonSerializer.Deserialize<IEnumerable<WorkItemResult>>(mcpResponse.Result.ToString()) 
                           ?? Enumerable.Empty<WorkItemResult>();
                }

                return Enumerable.Empty<WorkItemResult>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting work items via MCP");
                return Enumerable.Empty<WorkItemResult>();
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_mcpServerUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MCP server connection test failed");
                return false;
            }
        }
    }

    // MCP Protocol Models
    public class McpToolRequest
    {
        public string Tool { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class McpToolResponse
    {
        public bool Success { get; set; }
        public object? Result { get; set; }
        public string? Error { get; set; }
    }
}
