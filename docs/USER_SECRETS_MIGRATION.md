# User Secrets Migration - .env.template to Visual Studio User Secrets

## Overview

This project has been migrated from using `.env.template` files to Visual Studio User Secrets for local development secret management. This provides better security and integration with .NET development workflows.

## What Changed

### Before (.env.template approach):
- Secrets stored in `.env` files (risk of accidental commits)
- Manual environment variable loading
- Separate configuration for different environments

### After (User Secrets approach):
- Secrets stored securely by Visual Studio/dotnet CLI
- Automatic integration with .NET configuration system
- No risk of committing secrets to source control
- Better developer experience

## Migration Steps Completed

1. ✅ **Added User Secrets support to project files**
   - Added `<UserSecretsId>` to both TeamsBot.csproj and McpServer.csproj
   - Added `Microsoft.Extensions.Configuration.UserSecrets` package references

2. ✅ **Created appsettings.json files**
   - `McpServer/appsettings.json` - Production configuration template
   - `McpServer/appsettings.Development.json` - Development overrides
   - Existing TeamsBot configuration files remain unchanged

3. ✅ **Updated solution structure**
   - Added McpServer and McpServer.Tests projects to solution
   - Configured proper project references

4. ✅ **Created migration documentation**
   - `docs/USER_SECRETS_SETUP.md` - Complete setup guide
   - Migration mapping from .env variables to User Secrets paths

5. ✅ **Updated deployment configuration**
   - Modified `azure.yaml` to reflect new approach
   - Updated post-deployment messages to guide developers

## Next Steps for Developers

### 1. Initialize User Secrets for Each Project

```bash
# For TeamsBot (if not already done)
cd TeamsBot
dotnet user-secrets init

# For McpServer
cd McpServer
dotnet user-secrets init
```

### 2. Set Your Local Development Secrets

Follow the detailed instructions in `docs/USER_SECRETS_SETUP.md` to configure your secrets.

### 3. Remove .env Files (Optional)

The `.env.template` file can be kept as a reference but is no longer required for local development. You may choose to:
- Keep it for documentation purposes
- Remove it to avoid confusion
- Update it to reference User Secrets approach

## Benefits Gained

1. **Security**: Secrets are encrypted and stored outside the repository
2. **Developer Experience**: Seamless integration with Visual Studio and dotnet CLI
3. **No Accidents**: Impossible to accidentally commit secrets
4. **Per-User**: Each developer has their own isolated secrets
5. **Standard Practice**: Follows .NET development best practices

## Production Deployment

Production deployment is unaffected by this change:
- Azure Key Vault is used for production secrets
- Managed Identity handles authentication
- azd handles automated deployment
- Bicep templates remain the same

## Troubleshooting

If you encounter issues:
1. Ensure `<UserSecretsId>` is present in your `.csproj` files
2. Run `dotnet user-secrets list` to verify secrets are set
3. Check that `Microsoft.Extensions.Configuration.UserSecrets` package is referenced
4. See `docs/USER_SECRETS_SETUP.md` for detailed troubleshooting

## File Changes Summary

| File | Change | Status |
|------|--------|--------|
| `McpServer/McpServer.csproj` | Added User Secrets support | ✅ Created |
| `McpServer/appsettings.json` | Production configuration | ✅ Created |
| `McpServer/appsettings.Development.json` | Development overrides | ✅ Created |
| `McpServer.Tests/McpServer.Tests.csproj` | Test project with User Secrets | ✅ Created |
| `teams-ado-mcp.sln` | Added new projects | ✅ Updated |
| `azure.yaml` | Updated deployment messages | ✅ Updated |
| `docs/USER_SECRETS_SETUP.md` | Complete setup guide | ✅ Created |
| `.env.template` | Marked as reference only | ℹ️ Deprecated |

The migration is complete and ready for use!
