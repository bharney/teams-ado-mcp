# Deployment Guide - Teams-Azure DevOps MCP Integration

This guide provides comprehensive instructions for deploying the Teams-Azure DevOps MCP integration using either Azure Developer CLI (azd) or OneBranch pipelines.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Start with Azure Developer CLI](#quick-start-with-azure-developer-cli)
3. [Enterprise Deployment with OneBranch](#enterprise-deployment-with-onebranch)
4. [Configuration](#configuration)
5. [Monitoring and Troubleshooting](#monitoring-and-troubleshooting)

## Prerequisites

### Required Tools

- **Azure CLI** v2.50.0 or later
- **Azure Developer CLI (azd)** v1.5.0 or later (for azd deployment)
- **.NET 8.0 SDK** (for local development)
- **Docker** (for container builds)
- **PowerShell 7+** or **Bash** (for scripts)

### Azure Requirements

- **Azure Subscription** with appropriate permissions
- **Azure DevOps Organization** and **Project**
- **Azure DevOps Personal Access Token (PAT)** with work item permissions

### Permissions Required

- `Contributor` role on the Azure subscription or resource group
- `User Access Administrator` role for role assignments
- `Key Vault Administrator` role for Key Vault operations

## Quick Start with Azure Developer CLI

The fastest way to deploy the solution is using Azure Developer CLI (azd).

### 1. Install Azure Developer CLI

```bash
# Windows (PowerShell)
winget install Microsoft.Azd

# macOS
brew tap azure/azd && brew install azd

# Linux
curl -fsSL https://aka.ms/install-azd.sh | bash
```

### 2. Clone and Initialize

```bash
# Clone the repository
git clone https://github.com/your-org/teams-ado-mcp.git
cd teams-ado-mcp

# Initialize azd environment
azd init

# Follow prompts to set:
# - Environment name: teams-ado-mcp
# - Azure subscription
# - Azure region (e.g., East US)
```

### 3. Configure Environment Variables

```bash
# Set Azure DevOps configuration
azd env set AZURE_DEVOPS_PAT "your-azure-devops-pat"

# Optional: Set custom values
azd env set AZURE_LOCATION "eastus"
azd env set LOG_RETENTION_DAYS "30"
```

### 4. Deploy Infrastructure and Applications

```bash
# Deploy everything (infrastructure + applications)
azd up

# Or deploy step by step:
azd provision  # Deploy infrastructure only
azd deploy     # Deploy applications only
```

### 5. Verify Deployment

```bash
# Get application URLs
azd env get-values

# Test MCP Server
curl https://[MCP_SERVER_FQDN]/tools/list

# Test Teams Bot
curl https://[TEAMS_BOT_FQDN]/health
```

## Enterprise Deployment with OneBranch

For enterprise environments requiring enhanced security and compliance, use OneBranch pipelines.

### 1. Setup Azure DevOps Project

1. Create a new Azure DevOps project
2. Import this repository
3. Create service connections:
   - **Azure Resource Manager** connection for your subscription
   - **Container Registry** connection for ACR

### 2. Configure Pipeline Variables

In Azure DevOps, navigate to **Pipelines** → **Library** and create a variable group named `teams-ado-mcp-variables`:

| Variable Name | Value | Secret |
|---------------|-------|--------|
| `azureServiceConnection` | Name of your ARM service connection | No |
| `azureSubscription` | Your Azure subscription ID | No |
| `AZURE_DEVOPS_PAT` | Azure DevOps Personal Access Token | Yes |
| `containerRegistryName` | ACR name (will be created) | No |
| `location` | Azure region (e.g., eastus) | No |

### 3. Create Pipeline

1. In Azure DevOps, go to **Pipelines** → **New Pipeline**
2. Choose **Azure Repos Git**
3. Select your repository
4. Choose **Existing Azure Pipelines YAML file**
5. Select `/.pipelines/onebranch.yml`
6. Save and run the pipeline

### 4. Environment Approval

1. Navigate to **Pipelines** → **Environments**
2. Configure approval gates for:
   - `dev-infrastructure`
   - `dev-applications`
   - `staging-infrastructure` (if needed)
   - `staging-applications` (if needed)
   - `prod-infrastructure`
   - `prod-applications`

### 5. Monitor Deployment

1. Watch the pipeline execution in Azure DevOps
2. Review security scan results
3. Approve environment deployments
4. Verify applications are running

## Configuration

### Environment Variables

The following environment variables can be configured:

#### Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `AZURE_DEVOPS_PAT` | Azure DevOps Personal Access Token | `ghp_xxxxxxxxxxxxxxxxxxxx` |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID | `12345678-1234-1234-1234-123456789012` |
| `AZURE_LOCATION` | Azure region | `eastus` |

#### Optional Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `LOG_RETENTION_DAYS` | Log retention in days | `30` |
| `ENABLE_APPLICATION_INSIGHTS` | Enable Application Insights | `true` |
| `KEY_VAULT_ACCESS_OBJECT_ID` | Object ID for Key Vault access | Auto-detected |

### Azure DevOps PAT Permissions

Your Azure DevOps PAT needs the following scopes:

- **Work Items**: Read & write
- **Project and Team**: Read (for project access)

### Key Vault Secrets

The following secrets are automatically configured in Key Vault:

| Secret Name | Description |
|-------------|-------------|
| `azure-devops-pat` | Azure DevOps Personal Access Token |

## Monitoring and Troubleshooting

### Health Checks

Both applications expose health check endpoints:

- **MCP Server**: `https://[MCP_SERVER_FQDN]/health`
- **Teams Bot**: `https://[TEAMS_BOT_FQDN]/health`

### Logs and Monitoring

#### Azure Monitor

- **Log Analytics Workspace**: Centralized logging
- **Application Insights**: Application performance monitoring
- **Container Insights**: Container performance and logs

#### Useful Queries

```kusto
// MCP Server logs
ContainerAppConsoleLogs_CL
| where ContainerAppName_s contains "mcp-server"
| order by TimeGenerated desc

// Teams Bot logs
ContainerAppConsoleLogs_CL
| where ContainerAppName_s contains "teams-bot"
| order by TimeGenerated desc

// Error tracking
AppExceptions
| where TimeGenerated > ago(1h)
| summarize count() by ProblemId
```

### Common Issues

#### Issue: Container App Not Starting

**Symptoms**: Health checks failing, container restarts

**Solutions**:
1. Check container logs in Azure portal
2. Verify environment variables are set correctly
3. Ensure container image is accessible
4. Check resource limits (CPU/memory)

#### Issue: Key Vault Access Denied

**Symptoms**: Authentication errors in logs

**Solutions**:
1. Verify managed identity has Key Vault access
2. Check Key Vault access policies or RBAC
3. Ensure Key Vault allows the container app's subnet

#### Issue: Azure DevOps API Errors

**Symptoms**: Work item creation failing

**Solutions**:
1. Verify Azure DevOps PAT is valid and has correct permissions
2. Check Azure DevOps organization and project settings
3. Ensure PAT is correctly stored in Key Vault

### Performance Tuning

#### Container App Scaling

```bash
# Update scaling rules
az containerapp update \
  --name mcp-server-[token] \
  --resource-group rg-teams-ado-mcp \
  --min-replicas 2 \
  --max-replicas 10
```

#### Resource Allocation

```bash
# Update resource limits
az containerapp update \
  --name mcp-server-[token] \
  --resource-group rg-teams-ado-mcp \
  --cpu 1.0 \
  --memory 2Gi
```

## Security Best Practices

### 1. Network Security

- Use private endpoints for Key Vault and Container Registry
- Implement network security groups
- Consider Azure Front Door for public-facing endpoints

### 2. Identity and Access

- Use managed identities for all service-to-service authentication
- Implement least privilege access principles
- Regularly rotate secrets and certificates

### 3. Monitoring and Auditing

- Enable diagnostic settings for all Azure resources
- Set up alerts for security events
- Monitor access patterns and anomalies

### 4. Compliance

- Regular security assessments
- Keep dependencies updated
- Follow Microsoft Security Development Lifecycle (SDL)

## Support and Troubleshooting

For additional support:

1. Check the [troubleshooting guide](../docs/TROUBLESHOOTING.md)
2. Review [Azure Container Apps documentation](https://docs.microsoft.com/azure/container-apps/)
3. Consult [Azure DevOps API documentation](https://docs.microsoft.com/azure/devops/integrate/)

---

*Last Updated: June 27, 2025*  
*Version: 2.1.0 - Production Infrastructure*
