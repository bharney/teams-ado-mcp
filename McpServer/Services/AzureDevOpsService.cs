using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using McpServer.Configuration;
using McpServer.Models;
using McpServer.Services;

namespace McpServer.Services;

/// <summary>
/// Azure DevOps service implementation using SFI-compliant federated identity
/// Implements OptimizedAzureCredential for secure, credential-free Azure DevOps API access
/// </summary>
public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly AzureDevOpsOptions _options;
    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private readonly SemaphoreSlim _authSemaphore;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Azure DevOps REST API scope for token requests
    /// </summary>
    private const string AdoScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

    public AzureDevOpsService(
        ILogger<AzureDevOpsService> logger,
        IOptions<AzureDevOpsOptions> options,
        HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        // Use ChainedTokenCredential for SFI compliance instead of DefaultAzureCredential
        // This provides deterministic behavior and better performance in production
        _credential = new ChainedTokenCredential(
            new ManagedIdentityCredential(), // For Azure-hosted environments
            new AzureCliCredential(),        // For local development
            new VisualStudioCredential()     // For Visual Studio development
        );
        _authSemaphore = new SemaphoreSlim(1, 1);
        
        // Configure JSON serialization
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Configure HTTP client
    // Ensure BaseAddress has trailing slash for safe relative resolution (though we will use absolute URLs below)
    var baseUrl = _options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/";
    _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "McpServer/1.0");
        if (!_httpClient.DefaultRequestHeaders.Accept.Any(a => a.MediaType == "application/json"))
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        
        _logger.LogInformation("Initialized Azure DevOps service for organization: {Organization}, project: {Project}", 
            _options.Organization, _options.Project);
    }

    public async Task<WorkItemResult> CreateWorkItemAsync(WorkItemRequest request)
    {
    // Service no longer disposable; previous disposal guard removed
        
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger.LogInformation("Creating work item: {Title} of type {WorkItemType}", request.Title, request.WorkItemType);

        try
        {
            // Prepare the JSON Patch document for work item creation
            var patchDocument = new List<AdoWorkItemCreateRequest>
            {
                new() { Path = "/fields/System.Title", Value = request.Title },
                new() { Path = "/fields/System.Description", Value = request.Description ?? string.Empty }
            }; // Work item type provided via URL ($TYPE) – no need to patch System.WorkItemType

            // Add optional fields if provided
            if (!string.IsNullOrEmpty(request.Priority))
            {
                patchDocument.Add(new AdoWorkItemCreateRequest 
                { 
                    Path = "/fields/Microsoft.VSTS.Common.Priority", 
                    Value = request.Priority 
                });
            }

            if (!string.IsNullOrEmpty(request.AssignedTo))
            {
                patchDocument.Add(new AdoWorkItemCreateRequest 
                { 
                    Path = "/fields/System.AssignedTo", 
                    Value = request.AssignedTo 
                });
            }

            // Execute the API call with retry logic
            var response = await ExecuteWithRetryAsync(async () =>
            {
                // IMPORTANT: Do NOT prefix with leading '/' or HttpClient will drop the organization path segment in BaseAddress
                _logger.LogDebug("CreateWorkItem BaseUrl={BaseUrl} Project={Project} Type={Type}", _options.BaseUrl, _options.Project, request.WorkItemType);
                var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/${request.WorkItemType}?api-version={_options.ApiVersion}";
                _logger.LogDebug("POST work item URL (absolute): {Url}", url);
                var json = JsonSerializer.Serialize(patchDocument, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");

                var httpResponse = await _httpClient.PostAsync(url, content);
                if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Create work item received 401. Auth header present: {HasAuth}. Using PAT: {UsingPat}",
                        _httpClient.DefaultRequestHeaders.Authorization != null,
                        _httpClient.DefaultRequestHeaders.Authorization?.Scheme == "Basic");
                }
                return httpResponse;
            });

            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(response, responseContent);
            }

            var adoWorkItem = JsonSerializer.Deserialize<AdoWorkItemResponse>(responseContent, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize Azure DevOps response");

            var result = MapAdoWorkItemToResult(adoWorkItem);
            
            _logger.LogInformation("Successfully created work item with ID: {WorkItemId}", result.Id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create work item: {Title}", request.Title);
            throw;
        }
    }

    public async Task<WorkItemResult> GetWorkItemAsync(int id)
    {
    // Disposal guard removed
        
        _logger.LogInformation("Getting work item: {WorkItemId}", id);

        try
        {
            var response = await ExecuteWithRetryAsync(async () =>
            {
                var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{id}?api-version={_options.ApiVersion}";
                _logger.LogDebug("GET work item URL: {Url}", url);
                var httpResponse = await _httpClient.GetAsync(url);
                if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Get work item received 401. Auth header present: {HasAuth}. Scheme: {Scheme}",
                        _httpClient.DefaultRequestHeaders.Authorization != null,
                        _httpClient.DefaultRequestHeaders.Authorization?.Scheme);
                }
                return httpResponse;
            });

            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(response, responseContent);
            }

            var adoWorkItem = JsonSerializer.Deserialize<AdoWorkItemResponse>(responseContent, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize Azure DevOps response");

            var result = MapAdoWorkItemToResult(adoWorkItem);
            
            _logger.LogInformation("Successfully retrieved work item with ID: {WorkItemId}", result.Id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get work item: {WorkItemId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<WorkItemResult>> GetWorkItemsAsync(string? query = null)
    {
    // Disposal guard removed
        
        _logger.LogInformation("Getting work items with query: {Query}", query ?? "none");

        try
        {
            // Use default query if none provided
            var wiqlQuery = query ?? $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{_options.Project}' ORDER BY [System.Id] DESC";

            // Execute WIQL query first to get work item IDs
            var queryResponse = await ExecuteWithRetryAsync(async () =>
            {
                var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/wiql?api-version={_options.ApiVersion}";
                _logger.LogDebug("POST WIQL URL: {Url}", url);
                var queryRequest = new { query = wiqlQuery };
                var json = JsonSerializer.Serialize(queryRequest, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var httpResponse = await _httpClient.PostAsync(url, content);
                if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("WIQL query received 401. Auth header present: {HasAuth}. Scheme: {Scheme}",
                        _httpClient.DefaultRequestHeaders.Authorization != null,
                        _httpClient.DefaultRequestHeaders.Authorization?.Scheme);
                }
                return httpResponse;
            });

            var queryContent = await queryResponse.Content.ReadAsStringAsync();
            
            if (!queryResponse.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(queryResponse, queryContent);
            }

            var queryResult = JsonSerializer.Deserialize<AdoWorkItemsQueryResponse>(queryContent, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize Azure DevOps query response");

            if (!queryResult.WorkItems.Any())
            {
                _logger.LogInformation("No work items found for query");
                return Enumerable.Empty<WorkItemResult>();
            }

            // Get work item details in batches
            var workItemIds = queryResult.WorkItems.Take(50).Select(wi => wi.Id).ToList(); // Limit to 50 for performance
            var idsParam = string.Join(",", workItemIds);

            var detailsResponse = await ExecuteWithRetryAsync(async () =>
            {
                var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems?ids={idsParam}&api-version={_options.ApiVersion}";
                _logger.LogDebug("GET work items batch URL: {Url}", url);
                var httpResponse = await _httpClient.GetAsync(url);
                if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Get work item details received 401. Auth header present: {HasAuth}. Scheme: {Scheme}",
                        _httpClient.DefaultRequestHeaders.Authorization != null,
                        _httpClient.DefaultRequestHeaders.Authorization?.Scheme);
                }
                return httpResponse;
            });

            var detailsContent = await detailsResponse.Content.ReadAsStringAsync();
            
            if (!detailsResponse.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(detailsResponse, detailsContent);
            }

            var detailsResult = JsonSerializer.Deserialize<JsonElement>(detailsContent, _jsonOptions);
            var workItemsArray = detailsResult.GetProperty("value");

            var results = new List<WorkItemResult>();
            foreach (var workItemElement in workItemsArray.EnumerateArray())
            {
                var adoWorkItem = JsonSerializer.Deserialize<AdoWorkItemResponse>(workItemElement.GetRawText(), _jsonOptions);
                if (adoWorkItem != null)
                {
                    results.Add(MapAdoWorkItemToResult(adoWorkItem));
                }
            }

            _logger.LogInformation("Successfully retrieved {Count} work items", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get work items with query: {Query}", query);
            throw;
        }
    }

    public async Task<WorkItemResult> UpdateWorkItemAsync(int id, WorkItemRequest request)
    {
    // Disposal guard removed
        
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger.LogInformation("Updating work item: {WorkItemId}", id);

        try
        {
            // Prepare the JSON Patch document for work item update
            var patchDocument = new List<AdoWorkItemCreateRequest>();

            if (!string.IsNullOrEmpty(request.Title))
            {
                patchDocument.Add(new AdoWorkItemCreateRequest { Path = "/fields/System.Title", Value = request.Title });
            }

            if (!string.IsNullOrEmpty(request.Description))
            {
                patchDocument.Add(new AdoWorkItemCreateRequest { Path = "/fields/System.Description", Value = request.Description });
            }

            if (!string.IsNullOrEmpty(request.Priority))
            {
                patchDocument.Add(new AdoWorkItemCreateRequest 
                { 
                    Path = "/fields/Microsoft.VSTS.Common.Priority", 
                    Value = request.Priority 
                });
            }

            if (!string.IsNullOrEmpty(request.AssignedTo))
            {
                patchDocument.Add(new AdoWorkItemCreateRequest 
                { 
                    Path = "/fields/System.AssignedTo", 
                    Value = request.AssignedTo 
                });
            }

            if (!patchDocument.Any())
            {
                throw new ArgumentException("At least one field must be provided for update", nameof(request));
            }

            // Execute the API call with retry logic
            var response = await ExecuteWithRetryAsync(async () =>
            {
                var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{id}?api-version={_options.ApiVersion}";
                _logger.LogDebug("PATCH work item URL: {Url}", url);
                var json = JsonSerializer.Serialize(patchDocument, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");

                var httpResponse = await _httpClient.PatchAsync(url, content);
                if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Update work item received 401. Auth header present: {HasAuth}. Scheme: {Scheme}",
                        _httpClient.DefaultRequestHeaders.Authorization != null,
                        _httpClient.DefaultRequestHeaders.Authorization?.Scheme);
                }
                return httpResponse;
            });

            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(response, responseContent);
            }

            var adoWorkItem = JsonSerializer.Deserialize<AdoWorkItemResponse>(responseContent, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize Azure DevOps response");

            var result = MapAdoWorkItemToResult(adoWorkItem);
            
            _logger.LogInformation("Successfully updated work item with ID: {WorkItemId}", result.Id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update work item: {WorkItemId}", id);
            throw;
        }
    }

    /// <summary>
    /// Executes an HTTP request with exponential backoff retry logic for transient failures
    /// </summary>
    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<Task<HttpResponseMessage>> operation)
    {
        var attempt = 0;
        var delay = TimeSpan.FromMilliseconds(_options.InitialRetryDelayMs);

        while (attempt < _options.MaxRetryAttempts)
        {
            attempt++;
            
            try
            {
                // Ensure authentication token is set
                await EnsureAuthenticatedAsync();
                
                var response = await operation();
                
                // Check if retry is needed for transient failures
                if (IsTransientFailure(response))
                {
                    if (attempt < _options.MaxRetryAttempts)
                    {
                        _logger.LogWarning("Transient failure occurred (attempt {Attempt}/{MaxAttempts}). Status: {StatusCode}. Retrying in {Delay}ms",
                            attempt, _options.MaxRetryAttempts, response.StatusCode, delay.TotalMilliseconds);
                        
                        await Task.Delay(delay);
                        delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
                        continue;
                    }
                }
                
                return response;
            }
            catch (HttpRequestException ex) when (attempt < _options.MaxRetryAttempts)
            {
                _logger.LogWarning(ex, "HTTP request exception occurred (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}ms",
                    attempt, _options.MaxRetryAttempts, delay.TotalMilliseconds);
                
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < _options.MaxRetryAttempts)
            {
                _logger.LogWarning(ex, "Request timeout occurred (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}ms",
                    attempt, _options.MaxRetryAttempts, delay.TotalMilliseconds);
                
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
        }

        // Final attempt without retry
        await EnsureAuthenticatedAsync();
        return await operation();
    }

    /// <summary>
    /// Ensures the HTTP client has a valid Azure DevOps access token
    /// </summary>
    private async Task EnsureAuthenticatedAsync()
    {
        await _authSemaphore.WaitAsync();
        try
        {
            // Check if we already have a valid token (simple check)
            if (_httpClient.DefaultRequestHeaders.Authorization != null)
            {
                return; // Assume token is still valid - Azure.Identity handles refresh
            }

            // Local development fallback: support Personal Access Token from options (User Secrets) or environment variable (NOT for production)
            // Order of precedence:
            // 1. _options.PersonalAccessToken (User Secrets)
            // 2. AZDO_PAT environment variable
            // 3. AZURE_DEVOPS_EXT_PAT environment variable
            var pat = _options.PersonalAccessToken;
            if (string.IsNullOrWhiteSpace(pat))
            {
                pat = Environment.GetEnvironmentVariable("AZDO_PAT")
                      ?? Environment.GetEnvironmentVariable("AZURE_DEVOPS_EXT_PAT");
            }
            if (!string.IsNullOrWhiteSpace(pat))
            {
                // PAT basic auth: username can be anything non-empty; convention uses empty user or "pat"
                var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", basic);
                _logger.LogWarning("Using PAT authentication (development fallback). Source: {Source}", _options.PersonalAccessToken != null ? "UserSecrets" : "Environment");
                return;
            }

            _logger.LogDebug("Acquiring Azure DevOps access token using federated identity");

            var tokenRequest = new TokenRequestContext(new[] { AdoScope });
            var tokenResult = await _credential.GetTokenAsync(tokenRequest, CancellationToken.None);

            if (tokenResult.Token == null)
            {
                throw new InvalidOperationException("Failed to acquire access token for Azure DevOps");
            }

            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", tokenResult.Token);

            _logger.LogDebug("Successfully acquired Azure DevOps access token");
        }
        finally
        {
            _authSemaphore.Release();
        }
    }

    /// <summary>
    /// Determines if an HTTP response indicates a transient failure that should be retried
    /// </summary>
    private static bool IsTransientFailure(HttpResponseMessage response)
    {
        return response.StatusCode == HttpStatusCode.InternalServerError ||
               response.StatusCode == HttpStatusCode.BadGateway ||
               response.StatusCode == HttpStatusCode.ServiceUnavailable ||
               response.StatusCode == HttpStatusCode.GatewayTimeout ||
               response.StatusCode == HttpStatusCode.TooManyRequests;
    }

    /// <summary>
    /// Handles error responses from Azure DevOps API
    /// </summary>
    private Task HandleErrorResponseAsync(HttpResponseMessage response, string responseContent)
    {
        var statusCode = (int)response.StatusCode;
        
        try
        {
            var errorResponse = JsonSerializer.Deserialize<AdoErrorResponse>(responseContent, _jsonOptions);
            var errorMessage = errorResponse?.Message ?? response.ReasonPhrase ?? "Unknown error";
            
            _logger.LogError("Azure DevOps API error. Status: {StatusCode}, Message: {ErrorMessage}, Content: {ResponseContent}",
                statusCode, errorMessage, responseContent);

            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new UnauthorizedAccessException($"Authentication failed: {errorMessage}"),
                HttpStatusCode.Forbidden => new UnauthorizedAccessException($"Access denied: {errorMessage}"),
                HttpStatusCode.NotFound => new ArgumentException($"Resource not found: {errorMessage}"),
                HttpStatusCode.BadRequest => new ArgumentException($"Invalid request: {errorMessage}"),
                _ => new InvalidOperationException($"Azure DevOps API error ({statusCode}): {errorMessage}")
            };
        }
        catch (JsonException)
        {
            // If we can't parse the error response, use the raw content
            _logger.LogError("Azure DevOps API error. Status: {StatusCode}, Content: {ResponseContent}",
                statusCode, responseContent);
            
            throw new InvalidOperationException($"Azure DevOps API error ({statusCode}): {response.ReasonPhrase}");
        }
    }

    /// <summary>
    /// Maps Azure DevOps work item response to our domain model
    /// </summary>
    private static WorkItemResult MapAdoWorkItemToResult(AdoWorkItemResponse adoWorkItem)
    {
        return new WorkItemResult
        {
            Id = adoWorkItem.Id,
            Title = GetFieldValue<string>(adoWorkItem.Fields, "System.Title") ?? string.Empty,
            Description = GetFieldValue<string>(adoWorkItem.Fields, "System.Description"),
            WorkItemType = GetFieldValue<string>(adoWorkItem.Fields, "System.WorkItemType") ?? string.Empty,
            State = GetFieldValue<string>(adoWorkItem.Fields, "System.State") ?? string.Empty,
            Priority = GetFieldValue<string>(adoWorkItem.Fields, "Microsoft.VSTS.Common.Priority"),
            AssignedTo = GetFieldValue<string>(adoWorkItem.Fields, "System.AssignedTo")
        };
    }

    /// <summary>
    /// Helper method to safely extract field values from Azure DevOps response
    /// </summary>
    private static T? GetFieldValue<T>(Dictionary<string, object?> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var value) && value != null)
        {
            try
            {
                if (value is JsonElement jsonElement)
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                // Return default value if conversion fails
            }
        }
        return default;
    }

    // Disposal pattern removed to avoid premature disposal when tool instance is cached.
}
