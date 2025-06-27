using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace McpServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class McpController : ControllerBase
{
    private readonly ILogger<McpController> _logger;
    private readonly IConfiguration _configuration;

    public McpController(ILogger<McpController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("info")]
    public IActionResult GetInfo()
    {
        var mcpConfig = _configuration.GetSection("Mcp");
        var info = new
        {
            ServerName = mcpConfig["ServerName"] ?? "teams-ado-mcp-server",
            Version = mcpConfig["Version"] ?? "1.0.0",
            Description = mcpConfig["Description"] ?? "MCP server for Teams-Azure DevOps integration",
            Capabilities = new[] { "create-work-item", "list-work-items", "update-work-item" },
            Status = "running",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        };

        return Ok(info);
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    [HttpPost("test-secrets")]
    public IActionResult TestSecrets()
    {
        try
        {
            var adoConfig = _configuration.GetSection("AzureDevOps");
            var hasPatConfigured = !string.IsNullOrEmpty(adoConfig["PersonalAccessToken"]);
            var hasOrgConfigured = !string.IsNullOrEmpty(adoConfig["Organization"]);
            var hasProjectConfigured = !string.IsNullOrEmpty(adoConfig["Project"]);

            return Ok(new
            {
                message = "Configuration test completed",
                azureDevOps = new
                {
                    personalAccessTokenConfigured = hasPatConfigured,
                    organizationConfigured = hasOrgConfigured,
                    projectConfigured = hasProjectConfigured
                },
                configurationSource = "User Secrets (Development) / Key Vault (Production)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing configuration");
            return StatusCode(500, new { error = "Configuration test failed", message = ex.Message });
        }
    }
}
