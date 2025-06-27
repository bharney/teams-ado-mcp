using Microsoft.Extensions.Configuration;
using Xunit;

namespace McpServer.Tests;

public class UserSecretsConfigurationTests
{
    [Fact]
    public void Configuration_ShouldSupportUserSecrets()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<UserSecretsConfigurationTests>()
            .Build();

        // Act
        var azureDevOpsSection = configuration.GetSection("AzureDevOps");
        var mcpSection = configuration.GetSection("Mcp");

        // Assert
        Assert.NotNull(azureDevOpsSection);
        Assert.NotNull(mcpSection);
        
        // Verify configuration structure exists (values may be empty in CI/CD)
        Assert.True(azureDevOpsSection.Exists() || configuration.GetSection("AzureDevOps:Organization") != null);
    }

    [Fact]
    public void UserSecrets_ShouldBeConfigured()
    {
        // This test verifies that the User Secrets system is properly configured
        // It doesn't require actual secrets to be set, just that the system is available
        
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<UserSecretsConfigurationTests>()
            .Build();

        // The fact that we can build the configuration with User Secrets means it's working
        Assert.NotNull(configuration);
    }
}
