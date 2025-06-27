# User Secrets Configuration Guide

This project uses Visual Studio User Secrets for local development secret management instead of `.env` files. This approach provides better security and integrates seamlessly with .NET configuration.

## Setup Instructions

### For TeamsBot Project

1. **Initialize User Secrets (if not already done):**
   ```bash
   cd TeamsBot
   dotnet user-secrets init
   ```

2. **Set secrets using CLI:**
   ```bash
   # Azure DevOps configuration
   dotnet user-secrets set "AzureDevOps:PersonalAccessToken" "your-ado-pat-token"
   dotnet user-secrets set "AzureDevOps:Organization" "your-ado-org"
   dotnet user-secrets set "AzureDevOps:Project" "your-project"

   # Teams Bot configuration
   dotnet user-secrets set "MicrosoftAppId" "your-bot-app-id"
   dotnet user-secrets set "MicrosoftAppPassword" "your-bot-app-password"
   dotnet user-secrets set "MicrosoftAppTenantId" "your-tenant-id"

   # Azure AD configuration
   dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
   dotnet user-secrets set "AzureAd:ClientId" "your-client-id"
   dotnet user-secrets set "AzureAd:Domain" "your-domain.onmicrosoft.com"

   # Teams AI configuration
   dotnet user-secrets set "TeamsAi:OpenAiApiKey" "your-openai-api-key"

   # Identity configuration for local development
   dotnet user-secrets set "Identity:UserAssignedClientId" "your-managed-identity-client-id"
   ```

### For McpServer Project

1. **Initialize User Secrets:**
   ```bash
   cd McpServer
   dotnet user-secrets init
   ```

2. **Set secrets using CLI:**
   ```bash
   # Azure DevOps configuration
   dotnet user-secrets set "AzureDevOps:PersonalAccessToken" "your-ado-pat-token"
   dotnet user-secrets set "AzureDevOps:Organization" "your-ado-org"
   dotnet user-secrets set "AzureDevOps:Project" "your-project"

   # Identity configuration for local development
   dotnet user-secrets set "Identity:UserAssignedClientId" "your-managed-identity-client-id"

   # Key Vault configuration for local development
   dotnet user-secrets set "KeyVault:VaultUri" "https://your-keyvault.vault.azure.net/"
   ```

### Alternative: Using Visual Studio

1. **Right-click on the project in Solution Explorer**
2. **Select "Manage User Secrets"**
3. **Add your secrets in JSON format:**

```json
{
  "AzureDevOps": {
    "PersonalAccessToken": "your-ado-pat-token",
    "Organization": "your-ado-org",
    "Project": "your-project"
  },
  "MicrosoftAppId": "your-bot-app-id",
  "MicrosoftAppPassword": "your-bot-app-password",
  "MicrosoftAppTenantId": "your-tenant-id",
  "AzureAd": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "Domain": "your-domain.onmicrosoft.com"
  },
  "TeamsAi": {
    "OpenAiApiKey": "your-openai-api-key"
  },
  "Identity": {
    "UserAssignedClientId": "your-managed-identity-client-id"
  },
  "KeyVault": {
    "VaultUri": "https://your-keyvault.vault.azure.net/"
  }
}
```

## Migration from .env.template

The following environment variables from `.env.template` should be configured in User Secrets:

| .env.template Variable | User Secrets Path | Description |
|----------------------|-------------------|-------------|
| `AZURE_DEVOPS_PAT` | `AzureDevOps:PersonalAccessToken` | Azure DevOps Personal Access Token |
| `AZURE_SUBSCRIPTION_ID` | Not needed for local dev | Used by azd deployment |
| `AZURE_LOCATION` | Not needed for local dev | Used by azd deployment |
| `AZURE_ENV_NAME` | Not needed for local dev | Used by azd deployment |

## Security Benefits

1. **Secrets are stored outside the repository**
2. **Encrypted on disk by the OS**
3. **Automatically loaded in Development environment**
4. **No risk of accidentally committing secrets**
5. **Per-user and per-machine isolation**

## Deployment

For production deployment:
- Use Azure Key Vault with Managed Identity
- Set environment variables in Azure App Service
- Use azd for automated deployment with Bicep templates

The `azure.yaml` and Bicep templates handle production configuration automatically.

## Troubleshooting

### View current secrets:
```bash
dotnet user-secrets list
```

### Clear all secrets:
```bash
dotnet user-secrets clear
```

### Check if User Secrets is properly configured:
Look for `<UserSecretsId>` in your `.csproj` file.

### Secrets location:
- **Windows:** `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
- **macOS/Linux:** `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`
