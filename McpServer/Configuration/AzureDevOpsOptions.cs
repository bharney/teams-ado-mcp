using System.ComponentModel.DataAnnotations;

namespace McpServer.Configuration;

/// <summary>
/// Azure DevOps configuration for SFI-compliant federated identity
/// </summary>
public class AzureDevOpsOptions
{
    public const string SectionName = "AzureDevOps";

    /// <summary>
    /// Azure DevOps organization name (e.g., "myorg" for https://dev.azure.com/myorg)
    /// </summary>
    [Required]
    public string Organization { get; set; } = string.Empty;

    /// <summary>
    /// Azure DevOps project name
    /// </summary>
    [Required]
    public string Project { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for Azure DevOps REST API
    /// </summary>
    public string BaseUrl => $"https://dev.azure.com/{Organization}";

    /// <summary>
    /// API version to use for Azure DevOps REST API calls
    /// </summary>
    public string ApiVersion { get; set; } = "7.1";

    /// <summary>
    /// Timeout for HTTP requests in seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts for transient failures
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay for exponential backoff in milliseconds
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Optional Personal Access Token for local development fallback (NOT used in production). Leave empty in committed appsettings.
    /// Bound via User Secrets if provided.
    /// </summary>
    public string? PersonalAccessToken { get; set; }
}
