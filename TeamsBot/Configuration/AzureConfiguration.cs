using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace TeamsBot.Configuration
{
    /// <summary>
    /// Configuration service following Azure MCP patterns for secure credential management
    /// Uses Managed Identity and Key Vault for production environments
    /// Implements SFI-compliant federated identity patterns
    /// </summary>
    public class AzureConfiguration
    {
        public AzureAdOptions AzureAd { get; set; } = new();
        public BotConfiguration Bot { get; set; } = new();
        public AzureDevOpsConfiguration AzureDevOps { get; set; } = new();
        public McpServerConfiguration McpServer { get; set; } = new();
        public TeamsAiConfiguration TeamsAi { get; set; } = new();
        public IdentityConfiguration Identity { get; set; } = new();
    }

    public class AzureAdOptions
    {
        public string Instance { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Scopes { get; set; } = string.Empty;
        public string CallbackPath { get; set; } = string.Empty;
    }

    public class BotConfiguration
    {
        public string MicrosoftAppType { get; set; } = "MultiTenant";
        public string MicrosoftAppId { get; set; } = string.Empty;
        public string MicrosoftAppPassword { get; set; } = string.Empty;
        public string MicrosoftAppTenantId { get; set; } = string.Empty;
    }

    public class AzureDevOpsConfiguration
    {
        public string Organization { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string PersonalAccessToken { get; set; } = string.Empty;
        public string DefaultWorkItemType { get; set; } = "Task";
        public string DefaultAreaPath { get; set; } = string.Empty;
        public string DefaultIterationPath { get; set; } = string.Empty;
    }

    public class McpServerConfiguration
    {
        public string Url { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public bool EnableRetry { get; set; } = true;
        public int MaxRetryAttempts { get; set; } = 3;
    }

    /// <summary>
    /// Teams AI Library configuration for advanced conversation understanding
    /// </summary>
    public class TeamsAiConfiguration
    {
        public string OpenAiApiKey { get; set; } = string.Empty;
        public string OpenAiEndpoint { get; set; } = "https://api.openai.com/v1";
        public string Model { get; set; } = "gpt-4o-mini";
        public float Temperature { get; set; } = 0.1f;
        public int MaxTokens { get; set; } = 1000;
        public bool EnableIntentDetection { get; set; } = true;
        public bool EnableActionItemExtraction { get; set; } = true;
        public string SystemPrompt { get; set; } = "You are a helpful assistant that identifies facilitator prompts and action items in Teams meetings.";
    }

    /// <summary>
    /// SFI-compliant federated identity configuration following Azure MCP patterns
    /// </summary>
    public class IdentityConfiguration
    {
        public bool UseManagedIdentity { get; set; } = true;
        public string UserAssignedClientId { get; set; } = string.Empty;
        public bool EnableTokenCaching { get; set; } = true;
        public int TokenCacheExpirationMinutes { get; set; } = 55; // Refresh before 60min expiry
        public string[] RequiredScopes { get; set; } = Array.Empty<string>();
        public string Authority { get; set; } = string.Empty;
        public bool EnableMultiTenant { get; set; } = true;
    }

    /// <summary>
    /// Configuration provider that follows SFI-compliant patterns from Azure MCP
    /// Integrates with Azure Key Vault for secure credential storage
    /// </summary>
    public interface ISecureConfigurationProvider
    {
        Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
        Task<AzureConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default);
    }

    public class SecureConfigurationProvider : ISecureConfigurationProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SecureConfigurationProvider> _logger;
        private readonly SecretClient? _secretClient;
        private readonly DefaultAzureCredential _credential;

        public SecureConfigurationProvider(
            IConfiguration configuration,
            ILogger<SecureConfigurationProvider> logger,
            DefaultAzureCredential credential)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));

            // Initialize Key Vault client if configured
            var keyVaultUrl = configuration["KeyVaultUrl"];
            if (!string.IsNullOrEmpty(keyVaultUrl))
            {
                try
                {
                    _secretClient = new SecretClient(new Uri(keyVaultUrl), _credential);
                    _logger.LogInformation("Initialized Key Vault client for {KeyVaultUrl}", keyVaultUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Key Vault client");
                }
            }
        }

        public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
        {
            try
            {
                // First try to get from Key Vault (production pattern)
                if (_secretClient != null)
                {
                    _logger.LogDebug("Retrieving secret {SecretName} from Key Vault", secretName);
                    var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
                    return response.Value.Value;
                }

                // Fallback to configuration (development pattern)
                var configValue = _configuration[secretName];
                if (!string.IsNullOrEmpty(configValue))
                {
                    _logger.LogDebug("Retrieved secret {SecretName} from configuration", secretName);
                    return configValue;
                }

                _logger.LogWarning("Secret {SecretName} not found in Key Vault or configuration", secretName);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving secret {SecretName}", secretName);
                return string.Empty;
            }
        }

        public async Task<AzureConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var config = new AzureConfiguration();

                // Bind basic configuration from appsettings
                _configuration.GetSection("AzureAd").Bind(config.AzureAd);
                _configuration.GetSection("AzureDevOps").Bind(config.AzureDevOps);
                _configuration.GetSection("McpServer").Bind(config.McpServer);
                _configuration.GetSection("TeamsAi").Bind(config.TeamsAi);
                _configuration.GetSection("Identity").Bind(config.Identity);

                // Bind bot configuration
                config.Bot.MicrosoftAppType = _configuration["MicrosoftAppType"] ?? "MultiTenant";
                config.Bot.MicrosoftAppId = _configuration["MicrosoftAppId"] ?? string.Empty;
                config.Bot.MicrosoftAppTenantId = _configuration["MicrosoftAppTenantId"] ?? string.Empty;

                // Get sensitive values from Key Vault or secure configuration
                config.Bot.MicrosoftAppPassword = await GetSecretAsync("MicrosoftAppPassword", cancellationToken);
                config.AzureDevOps.PersonalAccessToken = await GetSecretAsync("AzureDevOpsPersonalAccessToken", cancellationToken);
                config.McpServer.ApiKey = await GetSecretAsync("McpServerApiKey", cancellationToken);
                config.TeamsAi.OpenAiApiKey = await GetSecretAsync("OpenAiApiKey", cancellationToken);

                // Set authority from AzureAd configuration if not explicitly set
                if (string.IsNullOrEmpty(config.Identity.Authority))
                {
                    config.Identity.Authority = $"{config.AzureAd.Instance}{config.AzureAd.TenantId}/v2.0";
                }

                _logger.LogInformation("Configuration loaded successfully with {SectionCount} sections", 5);
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration");
                throw;
            }
        }
    }

    /// <summary>
    /// Extension methods for configuring secure configuration provider
    /// </summary>
    public static class ConfigurationExtensions
    {
        public static IServiceCollection AddSecureConfiguration(this IServiceCollection services)
        {
            services.AddSingleton<ISecureConfigurationProvider, SecureConfigurationProvider>();
            return services;
        }
    }
}
