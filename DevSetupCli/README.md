# DevSetup CLI - Azure DevOps Authentication Helper

A command-line utility to help configure Azure DevOps authentication and User Secrets for the Teams-ADO MCP project.

## 🚀 Quick Start

### Option 1: Using the batch script (Windows)
```cmd
# Check current status
.\devsetup.bat status

# Get help with authentication
.\devsetup.bat login --organization your-ado-org

# Configure PAT after creating it
.\devsetup.bat pat --token your-pat-token --organization your-ado-org
```

### Option 2: Using dotnet run
```bash
# Check current status
dotnet run --project DevSetupCli -- status

# Get help with authentication  
dotnet run --project DevSetupCli -- login --organization your-ado-org

# Configure PAT after creating it
dotnet run --project DevSetupCli -- pat --token your-pat-token --organization your-ado-org
```

## 📋 Available Commands

### `login` - Azure DevOps Authentication Setup
Helps you create a Personal Access Token (PAT) in Azure DevOps.

```bash
# Interactive mode - will prompt for organization
devsetup login

# Specify organization
devsetup login --organization your-ado-org
```

**What it does:**
- Opens Azure DevOps PAT creation page in your browser
- Provides guidance on PAT configuration
- Shows you the next steps to configure your secrets

### `pat` - Configure Personal Access Token
Validates and configures your PAT in User Secrets for both projects.

```bash
devsetup pat --token YOUR_PAT_TOKEN --organization your-ado-org

# Optional: specify default project
devsetup pat --token YOUR_PAT_TOKEN --organization your-ado-org --project your-project
```

**What it does:**
- Tests the PAT against Azure DevOps API
- Configures User Secrets for both TeamsBot and McpServer projects
- Sets up organization and project configuration

### `secrets` - Manage User Secrets
Sub-commands for managing User Secrets configuration.

```bash
# List all configured secrets
devsetup secrets list

# Initialize User Secrets for projects
devsetup secrets init

# Clear all secrets (with confirmation)
devsetup secrets clear
```

### `status` - Check Configuration Status
Shows the current state of your development environment.

```bash
devsetup status
```

**What it shows:**
- .NET SDK version
- User Secrets configuration status for both projects
- Build status
- Next steps recommendations

## 🔐 Azure DevOps PAT Configuration

When creating your PAT, use these settings:

- **Name**: `Teams-ADO-MCP-Local-Dev`
- **Organization**: All accessible organizations
- **Expiration**: 30 days (recommended for development)
- **Scopes**: 
  - **Minimum**: Work Items (Read & Write)
  - **Recommended**: Full access (for development)

## 🛠️ Troubleshooting

### PAT Validation Fails
```bash
# Check if your PAT is valid
curl -u :YOUR_PAT_TOKEN https://dev.azure.com/YOUR_ORG/_apis/projects?api-version=7.0
```

### User Secrets Not Working
```bash
# Check if User Secrets is initialized
dotnet user-secrets list --project TeamsBot
dotnet user-secrets list --project McpServer

# Reinitialize if needed
devsetup secrets init
```

### Build Issues
```bash
# Check build status
dotnet build

# Restore packages
dotnet restore
```

## 📁 Configuration Locations

### User Secrets Storage
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
- **macOS/Linux**: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`

### Project User Secret IDs
- **TeamsBot**: `aspnet-TeamsBot-b6e387cf-bec5-48b4-86bd-9b7aa82e8666`
- **McpServer**: `aspnet-McpServer-a7d438ef-bfc6-49c5-97ce-8a8bb93f7788`
- **DevSetupCli**: `aspnet-DevSetupCli-b8e397ff-cfd6-50d6-a8df-9c9bb94f8899`

## 🔄 Typical Workflow

1. **First time setup:**
   ```bash
   # Check current status
   devsetup status
   
   # Initialize User Secrets if needed
   devsetup secrets init
   
   # Get help creating PAT
   devsetup login --organization your-ado-org
   ```

2. **After creating PAT in Azure DevOps:**
   ```bash
   # Configure the PAT
   devsetup pat --token YOUR_PAT_TOKEN --organization your-ado-org --project your-project
   
   # Verify configuration
   devsetup status
   ```

3. **Check secrets anytime:**
   ```bash
   devsetup secrets list
   ```

## 🏗️ Building the CLI

```bash
# Build the CLI project
dotnet build DevSetupCli

# Run with arguments
dotnet run --project DevSetupCli -- status
```

## 🤝 Contributing

The CLI source is in `DevSetupCli/Program.cs`. It uses:
- **System.CommandLine** for command parsing
- **Microsoft.Extensions.Configuration** for User Secrets integration
- **Azure DevOps REST API** for PAT validation

## 📚 Related Documentation

- [User Secrets Setup Guide](USER_SECRETS_SETUP.md)
- [User Secrets Migration Guide](USER_SECRETS_MIGRATION.md)
- [Azure DevOps REST API](https://docs.microsoft.com/en-us/rest/api/azure/devops/)
- [.NET User Secrets](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
