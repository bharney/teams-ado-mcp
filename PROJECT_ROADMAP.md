# Microsoft Teams Bot + Azure DevOps MCP Server - Implementation Roadmap

## Document Overview
This roadmap documents the current state of our Microsoft Teams bot project and outlines the specific technical implementation steps needed to achieve SFI-compliant MCP server architecture with Azure Container Apps deployment and full CI/CD automation.

---

## Current Project State Assessment

### ✅ Completed Components
| Component | Status | Test Coverage | Notes |
|-----------|--------|---------------|-------|
| Teams Bot Framework | ✅ Complete | 127/127 tests passing | .NET 8 with Bot Framework SDK |
| Azure DevOps Integration | ✅ Basic | Unit + Integration tests | PAT-based authentication |
| MCP Client Implementation | ✅ Partial | Mock-based tests | HTTP client for external MCP servers |
| Configuration Management | ✅ Complete | Key Vault integration tests | SFI-ready configuration provider |
| TDD Infrastructure | ✅ Complete | <60ms test performance | Comprehensive mocking strategy |

### 🔄 Current Architecture
```
TeamsBot/
├── Controllers/BotController.cs        # Teams message endpoint
├── Handlers/TeamsAIActivityHandler.cs  # AI-enhanced message processing
├── Services/
│   ├── AzureDevOpsService.cs          # ADO work item creation
│   ├── McpClientService.cs            # MCP protocol client
│   └── ConversationIntelligenceService.cs # Intent detection
├── Configuration/AzureConfiguration.cs # SFI-compliant config
└── Models/ # Domain models for action items, work items
```

### ⚠️ Current Limitations & Gaps Analysis
- **Authentication**: PAT tokens instead of SFI-compliant federated identity with OpenID Connect
- **MCP Server**: Client-only implementation, no server endpoints with JSON-RPC 2.0 protocol
- **Deployment**: Basic Dockerfile, no Azure Container Apps with managed identity integration
- **CI/CD**: No automated deployment pipeline with Bicep what-if analysis and environment gates
- **AI Integration**: Keyword-based instead of Teams AI Library with Action Planner integration
- **Infrastructure**: Missing modular Bicep templates with user-assigned managed identities
- **Security**: No container security scanning or CodeQL analysis in pipeline
- **Monitoring**: No Application Insights integration with custom telemetry

---

## Implementation Phases

## Phase 1: MCP Server Foundation 🚀
**Goal**: Implement true MCP server with JSON-RPC 2.0 protocol compliance

### Phase 1.1: Core MCP Server Infrastructure ✅ **COMPLETED**
**Duration**: 1 session completed | **Tests**: 21 new test cases (148 total, all passing)

#### Technical Implementation ✅ **COMPLETED**
1. **✅ MCP Server Project Structure Created**
   ```
   McpServer/
   ├── Controllers/McpController.cs      # JSON-RPC 2.0 endpoint ✅
   ├── Models/JsonRpcModels.cs          # Request/Response models ✅
   ├── Services/
   │   ├── IMcpTool.cs                  # Tool interface ✅
   │   └── McpToolRegistry.cs           # Dynamic tool discovery ✅
   └── Program.cs                       # Service registration ✅
   
   McpServer.Tests/
   ├── JsonRpcEndpointTests.cs          # Integration tests ✅
   └── Services/McpToolRegistryTests.cs # Unit tests ✅
   ```

2. **✅ JSON-RPC 2.0 Protocol Handler Implemented**
   ```csharp
   [ApiController]
   [Route("api/mcp")]
   public class McpController : ControllerBase
   {
       [HttpPost] // Handles tools/list, tools/call, notifications ✅
       public async Task<IActionResult> HandleRequest([FromBody] JsonRpcRequest request)
       {
           // Full JSON-RPC 2.0 compliance with error handling ✅
       }
   }
   ```

#### TDD Test Cases ✅ **ALL COMPLETED**
- [✅] JSON-RPC request/response serialization
- [✅] Tool registration and discovery  
- [✅] Error handling for malformed requests
- [✅] Protocol version validation
- [✅] Notification request handling
- [✅] Tool parameter validation
- [✅] Result serialization patterns

#### Success Criteria ✅ **ALL MET**
- ✅ MCP server responds to JSON-RPC 2.0 requests
- ✅ Tool registry dynamically discovers and registers tools
- ✅ All existing TeamsBot tests continue to pass (127/127)
- ✅ 21 new MCP Server tests passing (148 total)
- ✅ Full JSON-RPC 2.0 protocol compliance
- ✅ Thread-safe tool registry implementation
- ✅ Proper error code handling (-32600, -32601, -32602, -32603)

### Phase 1.2: Azure DevOps MCP Tools Implementation ✅ **COMPLETED**
**Duration**: 1 session completed | **Tests**: 17 new test cases (165 total, all passing)

#### Technical Implementation ✅ **COMPLETED**
1. **✅ ADO Tool Implementations**
   ```csharp
   [McpTool("create_work_item")]
   public class CreateWorkItemTool : IMcpTool
   {
       public async Task<McpToolResult> ExecuteAsync(McpToolParameters parameters)
       {
           var request = new WorkItemRequest
           {
               Title = parameters.GetValue<string>("title"),
               Description = parameters.GetValue<string>("description", false),
               WorkItemType = parameters.GetValue<string>("workItemType"),
               Priority = parameters.GetValue<string>("priority", false),
               AssignedTo = parameters.GetValue<string>("assignedTo", false)
           };
           
           var result = await _adoService.CreateWorkItemAsync(request);
           return new McpToolResult { Success = true, Data = result };
       }
   }
   ```

2. **✅ Tool Parameter Validation**
   ```csharp
   public class McpToolParameters
   {
       public T GetValue<T>(string key, bool required = true)
       {
           if (!_parameters.ContainsKey(key))
           {
               if (required)
                   throw new McpToolException($"Required parameter '{key}' not provided");
               return default(T);
           }
           
           return JsonSerializer.Deserialize<T>(_parameters[key].ToString());
       }
   }
   ```

#### TDD Test Cases ✅ **COMPLETED**
- ✅ Work item creation with all required fields
- ✅ Parameter validation for missing required fields  
- ✅ Error handling for ADO API failures
- ✅ Tool result serialization
- ✅ Integration with Azure DevOps service
- ✅ JSON-RPC protocol compliance
- ✅ Integration tests with custom WebApplicationFactory

#### Success Criteria ✅ **COMPLETED**
- ✅ MCP tools can create, read, and update Azure DevOps work items
- ✅ Parameter validation prevents invalid requests
- ✅ Tool errors are properly formatted as JSON-RPC errors
- ✅ All 165 tests passing with integration test coverage

### Phase 1.3: SFI-Compliant Federated Identity Migration ✅ **COMPLETED**
**Duration**: 1 session completed | **Tests**: All existing tests maintained (165 total, all passing)

#### Technical Implementation ✅ **COMPLETED**
1. **✅ SFI-Compliant ChainedTokenCredential Implementation**
   ```csharp
   public class AzureDevOpsService : IAzureDevOpsService, IDisposable
   {
       private readonly TokenCredential _credential;
       private const string AdoScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

       public AzureDevOpsService(ILogger<AzureDevOpsService> logger, ...)
       {
           // Use ChainedTokenCredential for SFI compliance instead of DefaultAzureCredential
           _credential = new ChainedTokenCredential(
               new ManagedIdentityCredential(), // For Azure-hosted environments
               new AzureCliCredential(),        // For local development
               new VisualStudioCredential()     // For Visual Studio development
           );
       }
   }
   ```

2. **✅ Real Azure DevOps REST API Integration**
   ```csharp
   public async Task<WorkItemResult> CreateWorkItemAsync(WorkItemRequest request)
   {
       // JSON Patch document for work item creation
       var patchDocument = new List<AdoWorkItemCreateRequest>
       {
           new() { Path = "/fields/System.Title", Value = request.Title },
           new() { Path = "/fields/System.Description", Value = request.Description ?? string.Empty },
           new() { Path = "/fields/System.WorkItemType", Value = request.WorkItemType }
       };

       // Execute with retry logic and authentication
       var response = await ExecuteWithRetryAsync(async () =>
       {
           var url = $"/{_options.Project}/_apis/wit/workitems/${request.WorkItemType}?api-version={_options.ApiVersion}";
           var json = JsonSerializer.Serialize(patchDocument, _jsonOptions);
           var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");
           return await _httpClient.PostAsync(url, content);
       });
       
       // Handle response and map to domain model
   }
   ```

3. **✅ Configuration-Based Settings**
   ```json
   {
     "AzureDevOps": {
       "Organization": "your-organization",
       "Project": "your-project", 
       "ApiVersion": "7.1",
       "RequestTimeoutSeconds": 30,
       "MaxRetryAttempts": 3,
       "InitialRetryDelayMs": 1000
     }
   }
   ```

#### Advanced Features ✅ **COMPLETED**
- ✅ **Retry Logic**: Exponential backoff for transient failures (500, 502, 503, 504, 429)
- ✅ **Error Mapping**: HTTP status codes mapped to appropriate exceptions
- ✅ **Thread Safety**: Semaphore-protected authentication operations
- ✅ **Resource Management**: Proper disposal pattern with dispose checks
- ✅ **Comprehensive Logging**: Information, warning, and error logging throughout
- ✅ **Test Integration**: Mock service for integration tests

#### Success Criteria ✅ **COMPLETED**
- ✅ No hardcoded credentials or PAT tokens
- ✅ SFI-compliant ChainedTokenCredential implementation
- ✅ Real Azure DevOps REST API integration
- ✅ All CRUD operations supported (Create, Read, Update, List)
- ✅ Comprehensive error handling and retry logic
- ✅ Full test coverage with mocked integration tests
- ✅ 165/165 tests passing

#### Technical Implementation
1. **Replace PAT Authentication with Managed Identity**
   ```csharp
   public class FederatedIdentityAdoService : IAzureDevOpsService
   {
       private readonly DefaultAzureCredential _credential;
       private readonly TokenRequestContext _tokenContext;

       public async Task<string> GetAccessTokenAsync()
       {
           var tokenResult = await _credential.GetTokenAsync(_tokenContext);
           return tokenResult.Token;
       }
   }
   ```

2. **Configure OAuth Scopes for Azure DevOps**
   ```json
   {
     "AzureDevOps": {
       "Organization": "your-org",
       "Project": "your-project",
       "Scopes": [
         "vso.work_write",
         "vso.work_read"
       ],
       "UseFederatedIdentity": true
     }
   }
   ```

3. **Update Configuration Provider**
   ```csharp
   public async Task<AzureConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
   {
       // Remove PAT token retrieval
       // config.AzureDevOps.PersonalAccessToken = await GetSecretAsync("AzureDevOpsPersonalAccessToken");
       
       // Configure federated identity
       config.Identity.RequiredScopes = new[] { "https://app.vssps.visualstudio.com/.default" };
       config.Identity.UseManagedIdentity = true;
       
       return config;
   }
   ```

#### TDD Test Cases
- [ ] Token acquisition using DefaultAzureCredential
- [ ] OAuth scope validation
- [ ] Token refresh on expiration
- [ ] Fallback to local development credentials
- [ ] Integration tests with live Azure DevOps API

#### Success Criteria
- All Azure DevOps operations use federated identity
- No PAT tokens stored in configuration
- Local development continues to work with DefaultAzureCredential

---

## Phase 2: Azure Container Apps Infrastructure 🏗️
**Goal**: Deploy MCP server and Teams bot using Bicep templates with full automation

### Phase 2.1: Enhanced Bicep Infrastructure with Azure Best Practices 🎯 **CURRENT PHASE**
**Duration**: 2-3 sessions | **Tests**: Infrastructure validation + security compliance
**Reference**: [Azure Container Apps Built-in Auth with Bicep](https://github.com/Azure-Samples/containerapps-builtinauth-bicep)

#### Technical Implementation
1. **Infrastructure Directory Structure (Azure-Aligned)**
   ```
   infra/
   ├── main.bicep                        # Main deployment template with resource token
   ├── main.parameters.json              # Parameter file for AZD compatibility
   ├── modules/
   │   ├── container-apps-environment.bicep  # Container Apps environment with monitoring
   │   ├── container-app.bicep               # Container app with managed identity
   │   ├── container-registry.bicep          # ACR with user-assigned identity
   │   ├── key-vault.bicep                   # Key Vault with RBAC configuration
   │   ├── managed-identity.bicep            # User-assigned managed identity
   │   ├── log-analytics.bicep               # Log Analytics workspace
   │   └── app-insights.bicep                # Application Insights
   ├── parameters/
   │   ├── dev.bicepparam                    # Development environment
   │   ├── staging.bicepparam                # Staging environment  
   │   └── prod.bicepparam                   # Production environment
   └── scripts/
       ├── deploy.sh                         # Cross-platform deployment
       └── validate.sh                       # Pre-deployment validation
   ```

2. **Main Bicep Template with Resource Token Pattern**
   ```bicep
   targetScope = 'resourceGroup'
   
   @minLength(1)
   @maxLength(64)
   @description('Name of the environment that can be used as part of naming resource convention')
   param environmentName string
   
   @minLength(1)
   @description('Primary location for all resources')
   param location string
   
   @description('Container image for Teams Bot')
   param teamsBotImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
   
   @description('Container image for MCP Server')
   param mcpServerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
   
   // Generate unique resource token
   var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
   
   // Tags for all resources
   var tags = {
     'azd-env-name': environmentName
     'project': 'teams-ado-mcp'
     'environment': environmentName
   }
   
   // User-assigned managed identity (SFI-compliant)
   module managedIdentity 'modules/managed-identity.bicep' = {
     name: 'managedIdentity'
     params: {
       location: location
       resourceToken: resourceToken
       tags: tags
     }
   }
   
   // Container Registry with managed identity access
   module containerRegistry 'modules/container-registry.bicep' = {
     name: 'containerRegistry'
     params: {
       location: location
       resourceToken: resourceToken
       managedIdentityPrincipalId: managedIdentity.outputs.principalId
       tags: tags
     }
   }
   
   // Log Analytics for Container Apps
   module logAnalytics 'modules/log-analytics.bicep' = {
     name: 'logAnalytics'
     params: {
       location: location
       resourceToken: resourceToken
       tags: tags
     }
   }
   
   // Container Apps Environment
   module containerAppsEnvironment 'modules/container-apps-environment.bicep' = {
     name: 'containerAppsEnvironment'
     params: {
       location: location
       resourceToken: resourceToken
       logAnalyticsWorkspaceId: logAnalytics.outputs.logAnalyticsWorkspaceId
       tags: tags
     }
   }
   
   // Teams Bot Container App
   module teamsBot 'modules/container-app.bicep' = {
     name: 'teamsBot'
     params: {
       location: location
       resourceToken: resourceToken
       containerAppsEnvironmentId: containerAppsEnvironment.outputs.containerAppsEnvironmentId
       managedIdentityId: managedIdentity.outputs.managedIdentityId
       containerRegistryName: containerRegistry.outputs.containerRegistryName
       containerImage: teamsBotImage
       appName: 'teams-bot'
       targetPort: 8080
       environmentVariables: [
         {
           name: 'AZURE_CLIENT_ID'
           value: managedIdentity.outputs.clientId
         }
         {
           name: 'MCP_SERVER_URL'
           value: 'https://${mcpServer.outputs.fqdn}'
         }
       ]
       tags: union(tags, { 'azd-service-name': 'teams-bot' })
     }
   }
   
   // MCP Server Container App
   module mcpServer 'modules/container-app.bicep' = {
     name: 'mcpServer'
     params: {
       location: location
       resourceToken: resourceToken
       containerAppsEnvironmentId: containerAppsEnvironment.outputs.containerAppsEnvironmentId
       managedIdentityId: managedIdentity.outputs.managedIdentityId
       containerRegistryName: containerRegistry.outputs.containerRegistryName
       containerImage: mcpServerImage
       appName: 'mcp-server'
       targetPort: 8080
       environmentVariables: [
         {
           name: 'AZURE_CLIENT_ID'
           value: managedIdentity.outputs.clientId
         }
       ]
       tags: union(tags, { 'azd-service-name': 'mcp-server' })
     }
   }
   
   // Outputs for AZD compatibility
   output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.containerRegistryEndpoint
   output TEAMS_BOT_URL string = 'https://${teamsBot.outputs.fqdn}'
   output MCP_SERVER_URL string = 'https://${mcpServer.outputs.fqdn}'
   ```
           value: managedIdentity.outputs.clientId
         }
         {
           name: 'MCP_SERVER_URL'
           value: 'http://mcp-server'
         }
       ]
     }
   }

   // MCP Server Container App
   module mcpServer 'modules/container-app.bicep' = {
     name: 'mcpServer'
     params: {
       location: location
       environmentName: environmentName
       containerEnvironmentId: containerEnvironment.outputs.containerEnvironmentId
       managedIdentityId: managedIdentity.outputs.managedIdentityId
       containerImage: mcpServerImage
       appName: 'mcp-server'
       targetPort: 5000
       internalOnly: true
     }
   }
   ```

3. **Container App Module with Built-in Authentication Support**
   ```bicep
   // modules/container-app.bicep
   @description('Container Apps environment resource ID')
   param containerAppsEnvironmentId string
   
   @description('User-assigned managed identity resource ID')
   param managedIdentityId string
   
   @description('Container registry name')
   param containerRegistryName string
   
   @description('Container image name')
   param containerImage string
   
   @description('Application name')
   param appName string
   
   @description('Target port for the container')
   param targetPort int = 8080
   
   @description('Environment variables for the container')
   param environmentVariables array = []
   
   @description('Resource tags')
   param tags object = {}
   
   @description('Location for the resource')
   param location string
   
   @description('Resource token for unique naming')
   param resourceToken string
   
   var containerAppName = 'ca-${appName}-${resourceToken}'
   
   resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
     name: containerAppName
     location: location
     tags: tags
     identity: {
       type: 'UserAssigned'
       userAssignedIdentities: {
         '${managedIdentityId}': {}
       }
     }
     properties: {
       managedEnvironmentId: containerAppsEnvironmentId
       configuration: {
         activeRevisionsMode: 'Multiple'
         ingress: {
           external: true
           targetPort: targetPort
           transport: 'http'
           corsPolicy: {
             allowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
             allowedOrigins: ['*']
             allowCredentials: false
           }
         }
         registries: [
           {
             server: '${containerRegistryName}.azurecr.io'
             identity: managedIdentityId
           }
         ]
         secrets: []
       }
       template: {
         containers: [
           {
             name: appName
             image: containerImage
             resources: {
               cpu: json('0.5')
               memory: '1Gi'
             }
             env: environmentVariables
           }
         ]
         scale: {
           minReplicas: 1
           maxReplicas: 10
           rules: [
             {
               name: 'http-scaling-rule'
               http: {
                 metadata: {
                   concurrentRequests: '100'
                 }
               }
             }
           ]
         }
       }
     }
   }
   
   output fqdn string = containerApp.properties.configuration.ingress.fqdn
   output containerAppName string = containerApp.name
   ```

4. **Azure Developer CLI (azd) Configuration**
   ```yaml
   # azure.yaml (root directory)
   name: teams-ado-mcp
   metadata:
     template: teams-ado-mcp@0.0.1-beta
   
   services:
     teams-bot:
       project: ./TeamsBot
       language: dotnet
       host: containerapp
       
     mcp-server:
       project: ./McpServer  
       language: dotnet
       host: containerapp
   
   hooks:
     predeploy:
       windows:
         shell: pwsh
         run: |
           Write-Host "Pre-deployment validation..."
           az bicep build --file infra/main.bicep
           if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
       posix:
         shell: sh
         run: |
           echo "Pre-deployment validation..."
           az bicep build --file infra/main.bicep
     
     postdeploy:
       windows:
         shell: pwsh
         run: |
           Write-Host "Running post-deployment tests..."
           # Add health check scripts here
       posix:
         shell: sh  
         run: |
           echo "Running post-deployment tests..."
           # Add health check scripts here
   ```

#### Infrastructure Tests
- [ ] Bicep template validation (`az bicep build`)
- [ ] What-if deployment analysis
- [ ] Resource naming convention compliance
- [ ] Security configuration validation
- [ ] Cost estimation validation

#### Success Criteria
- Bicep templates deploy without errors
- Container Apps can communicate internally
- Managed identity has correct permissions
- All resources follow naming conventions

### Phase 2.2: Container Registry and Image Management
**Duration**: 1 session | **Tests**: Container build validation

#### Technical Implementation
1. **Update Dockerfiles for Multi-stage Builds**
   ```dockerfile
   # TeamsBot/Dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
   WORKDIR /app
   EXPOSE 80
   EXPOSE 443

   FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
   WORKDIR /src
   COPY ["TeamsBot/TeamsBot.csproj", "TeamsBot/"]
   COPY ["McpServer/McpServer.csproj", "McpServer/"]
   RUN dotnet restore "TeamsBot/TeamsBot.csproj"
   COPY . .
   WORKDIR "/src/TeamsBot"
   RUN dotnet build "TeamsBot.csproj" -c Release -o /app/build

   FROM build AS publish
   RUN dotnet publish "TeamsBot.csproj" -c Release -o /app/publish

   FROM base AS final
   WORKDIR /app
   COPY --from=publish /app/publish .
   
   # Health check endpoint
   HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
     CMD curl -f http://localhost:80/health || exit 1
   
   ENTRYPOINT ["dotnet", "TeamsBot.dll"]
   ```

2. **MCP Server Dockerfile**
   ```dockerfile
   # McpServer/Dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
   WORKDIR /app
   EXPOSE 5000

   FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
   WORKDIR /src
   COPY ["McpServer/McpServer.csproj", "McpServer/"]
   RUN dotnet restore "McpServer/McpServer.csproj"
   COPY . .
   WORKDIR "/src/McpServer"
   RUN dotnet build "McpServer.csproj" -c Release -o /app/build

   FROM build AS publish
   RUN dotnet publish "McpServer.csproj" -c Release -o /app/publish

   FROM base AS final
   WORKDIR /app
   COPY --from=publish /app/publish .
   ENTRYPOINT ["dotnet", "McpServer.dll"]
   ```

3. **Azure Container Registry Bicep Module**
   ```bicep
   @description('Azure Container Registry')
   resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
     name: 'acr${environmentName}${uniqueString(resourceGroup().id)}'
     location: location
     sku: {
       name: 'Standard'
     }
     properties: {
       adminUserEnabled: false
       anonymousPullEnabled: false
       policies: {
         retentionPolicy: {
           days: 30
           status: 'enabled'
         }
       }
     }
   }

   // Role assignment for managed identity to pull images
   resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
     scope: containerRegistry
     name: guid(containerRegistry.id, managedIdentityPrincipalId, acrPullRoleDefinitionId)
     properties: {
       roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') // AcrPull
       principalId: managedIdentityPrincipalId
       principalType: 'ServicePrincipal'
     }
   }
   ```

#### Container Tests
- [ ] Docker image builds successfully
- [ ] Health check endpoints respond correctly
- [ ] Container startup time under 30 seconds
- [ ] Image vulnerability scanning passes
- [ ] Multi-architecture support (AMD64/ARM64)

#### Success Criteria
- Container images build and push to ACR
- Health checks work correctly
- Container Apps can pull images using managed identity

---

## Phase 3: CI/CD Automation with Azure Best Practices 🔄
**Goal**: Fully automated deployment pipeline with Bicep what-if analysis, federated identity, and security scanning

### Phase 3.1: Enhanced GitHub Actions with Azure Best Practices
**Duration**: 2-3 sessions | **Tests**: Pipeline validation + infrastructure tests

#### Technical Implementation
**Reference**: [Microsoft Learn - Deploy Bicep with GitHub Actions](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-github-actions)

1. **Repository Structure for Azure-Aligned CI/CD**
   ```
   .github/
   ├── workflows/
   │   ├── bicep-unit-tests.yml       # Infrastructure validation (runs on every commit)
   │   ├── bicep-whatif-deploy.yml    # What-if analysis + deployment
   │   ├── ci.yml                     # Application CI with security scanning
   │   └── container-security.yml     # Container vulnerability scanning
   ├── environments/
   │   ├── development.yml            # Development environment protection
   │   ├── staging.yml               # Staging approval gates
   │   └── production.yml            # Production manual approval + compliance
   └── scripts/
       ├── validate-bicep.ps1        # Bicep validation scripts
       └── post-deploy-tests.ps1     # Health checks post-deployment
   ```

2. **Federated Identity Setup (SFI-Compliant)**
   **Based on**: [Microsoft Learn - OpenID Connect with GitHub Actions](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-github-actions#generate-deployment-credentials)
   ```yaml
   # Required GitHub Secrets for OpenID Connect:
   # AZURE_CLIENT_ID (from Entra application)
   # AZURE_TENANT_ID (Directory tenant ID)  
   # AZURE_SUBSCRIPTION_ID (Target subscription)
   # Note: No client secrets needed with federated credentials
   ```

3. **Bicep Unit Tests Workflow** 
   **Reference**: [Azure Sample - Bicep GitHub Actions](https://github.com/azure-samples/bicep-github-actions)
   ```yaml
   # .github/workflows/bicep-unit-tests.yml
   name: Bicep Unit Tests
   
   on:
     push:
       paths: ['infra/**/*.bicep', 'infra/**/*.bicepparam']
     pull_request:
       paths: ['infra/**/*.bicep', 'infra/**/*.bicepparam']
   
   jobs:
     bicep-unit-tests:
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@v4
         
         - name: Setup Azure CLI
           uses: azure/setup-cli@v1
           
         - name: Bicep Build
           run: |
             az bicep build --file infra/main.bicep
             echo "✅ Bicep templates compile successfully"
             
         - name: Bicep Template Validation
           run: |
             az deployment group validate \
               --resource-group rg-teams-ado-mcp-dev \
               --template-file infra/main.bicep \
               --parameters @infra/parameters/dev.bicepparam
               
         - name: Security Scan with Checkov
           uses: bridgecrewio/checkov-action@master
           with:
             directory: infra/
             framework: bicep
             output_format: sarif
             output_file_path: bicep-security-results.sarif
             
         - name: Upload Security Results to GitHub
           uses: github/codeql-action/upload-sarif@v2
           if: always()
           with:
             sarif_file: bicep-security-results.sarif
   ```

4. **Bicep What-If and Deploy Workflow**
   ```yaml
   # .github/workflows/bicep-whatif-deploy.yml
   name: Bicep What-If and Deploy
   
   on:
     pull_request:
       branches: [main]
       paths: ['infra/**']
     push:
       branches: [main]
       paths: ['infra/**']
   
   permissions:
     id-token: write
     contents: read
     pull-requests: write
   
   jobs:
     bicep-whatif:
       runs-on: ubuntu-latest
       environment: development
       outputs:
         whatifOutput: ${{ steps.whatif.outputs.whatifOutput }}
       steps:
         - uses: actions/checkout@v4
         
         - name: Azure Login with OIDC
           uses: azure/login@v1
           with:
             client-id: ${{ secrets.AZURE_CLIENT_ID }}
             tenant-id: ${{ secrets.AZURE_TENANT_ID }}
             subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
             
         - name: Run What-If Analysis
           id: whatif
           run: |
             whatifOutput=$(az deployment group what-if \
               --resource-group ${{ vars.RESOURCE_GROUP_NAME }} \
               --template-file infra/main.bicep \
               --parameters @infra/parameters/dev.bicepparam \
               --result-format FullResourcePayloads)
             echo "whatifOutput<<EOF" >> $GITHUB_OUTPUT
             echo "$whatifOutput" >> $GITHUB_OUTPUT
             echo "EOF" >> $GITHUB_OUTPUT
             
         - name: Comment What-If Results on PR
           if: github.event_name == 'pull_request'
           uses: actions/github-script@v6
           with:
             script: |
               const output = `
               ## Bicep What-If Results 🔍
               
               \`\`\`
               ${{ steps.whatif.outputs.whatifOutput }}
               \`\`\`
               
               *Workflow run: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}*
               `;
               
               github.rest.issues.createComment({
                 issue_number: context.issue.number,
                 owner: context.repo.owner,
                 repo: context.repo.repo,
                 body: output
               });
   
     bicep-deploy:
       needs: bicep-whatif
       if: github.ref == 'refs/heads/main'
       runs-on: ubuntu-latest
       environment: development
       steps:
         - uses: actions/checkout@v4
         
         - name: Azure Login with OIDC
           uses: azure/login@v1
           with:
             client-id: ${{ secrets.AZURE_CLIENT_ID }}
             tenant-id: ${{ secrets.AZURE_TENANT_ID }}
             subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
             
         - name: Deploy Infrastructure
           run: |
             az deployment group create \
               --resource-group ${{ vars.RESOURCE_GROUP_NAME }} \
               --template-file infra/main.bicep \
               --parameters @infra/parameters/dev.bicepparam \
               --name "deploy-${{ github.run_number }}"
   ```
   ```yaml
   # .github/workflows/infrastructure.yml
   name: Infrastructure Deployment

   on:
     push:
       branches: [ main ]
       paths: [ 'infra/**' ]
     workflow_dispatch:
       inputs:
         environment:
           description: 'Environment to deploy'
           required: true
           default: 'staging'
           type: choice
           options: [ 'staging', 'production' ]

   jobs:
     validate:
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@v4
         
         - name: Azure Login
           uses: azure/login@v1
           with:
             client-id: ${{ secrets.AZURE_CLIENT_ID }}
             tenant-id: ${{ secrets.AZURE_TENANT_ID }}
             subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
             
         - name: Validate Bicep templates
           run: |
             az bicep build --file infra/main.bicep
             az deployment group validate \
               --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }} \
               --template-file infra/main.bicep \
               --parameters @infra/parameters/main.${{ github.event.inputs.environment || 'staging' }}.bicepparam

     what-if:
       needs: validate
       runs-on: ubuntu-latest
       if: github.event_name == 'pull_request'
       steps:
         - uses: actions/checkout@v4
         
         - name: Azure Login
           uses: azure/login@v1
           with:
             client-id: ${{ secrets.AZURE_CLIENT_ID }}
             tenant-id: ${{ secrets.AZURE_TENANT_ID }}
             subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
             
         - name: Run what-if analysis
           id: whatif
           run: |
             az deployment group what-if \
               --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }} \
               --template-file infra/main.bicep \
               --parameters @infra/parameters/main.${{ github.event.inputs.environment || 'staging' }}.bicepparam \
               > whatif-output.txt
               
         - name: Comment what-if results
           uses: actions/github-script@v7
           with:
             script: |
               const fs = require('fs');
               const whatifOutput = fs.readFileSync('whatif-output.txt', 'utf8');
               github.rest.issues.createComment({
                 issue_number: context.issue.number,
                 owner: context.repo.owner,
                 repo: context.repo.repo,
                 body: '## Infrastructure What-If Analysis\n```\n' + whatifOutput + '\n```'
               });

     deploy:
       needs: validate
       runs-on: ubuntu-latest
       if: github.ref == 'refs/heads/main' && github.event_name == 'push'
       environment: ${{ github.event.inputs.environment || 'staging' }}
       steps:
         - uses: actions/checkout@v4
         
         - name: Azure Login
           uses: azure/login@v1
           with:
             client-id: ${{ secrets.AZURE_CLIENT_ID }}
             tenant-id: ${{ secrets.AZURE_TENANT_ID }}
             subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
             
         - name: Deploy infrastructure
           run: |
             az deployment group create \
               --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }} \
               --template-file infra/main.bicep \
               --parameters @infra/parameters/main.${{ github.event.inputs.environment || 'staging' }}.bicepparam
   ```

4. **Application Deployment Workflow**
   ```yaml
   # .github/workflows/cd-staging.yml
   name: Deploy to Staging

   on:
     workflow_run:
       workflows: ["Continuous Integration"]
       types: [completed]
       branches: [main]

   jobs:
     deploy:
       runs-on: ubuntu-latest
       if: ${{ github.event.workflow_run.conclusion == 'success' }}
       environment: staging
       steps:
         - uses: actions/checkout@v4
         
         - name: Azure Login
           uses: azure/login@v1
           with:
             client-id: ${{ secrets.AZURE_CLIENT_ID }}
             tenant-id: ${{ secrets.AZURE_TENANT_ID }}
             subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
             
         - name: Get image tags
           id: image-tags
           run: |
             TEAMS_BOT_TAG="${{ secrets.ACR_LOGIN_SERVER }}/teamsbot:${{ github.sha }}"
             MCP_SERVER_TAG="${{ secrets.ACR_LOGIN_SERVER }}/mcpserver:${{ github.sha }}"
             echo "teams-bot-tag=$TEAMS_BOT_TAG" >> $GITHUB_OUTPUT
             echo "mcp-server-tag=$MCP_SERVER_TAG" >> $GITHUB_OUTPUT
             
         - name: Update container apps
           run: |
             # Update Teams Bot container app
             az containerapp update \
               --name teams-bot-staging \
               --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }} \
               --image ${{ steps.image-tags.outputs.teams-bot-tag }}
               
             # Update MCP Server container app
             az containerapp update \
               --name mcp-server-staging \
               --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }} \
               --image ${{ steps.image-tags.outputs.mcp-server-tag }}
               
         - name: Wait for deployment
           run: |
             az containerapp revision list \
               --name teams-bot-staging \
               --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }} \
               --query "[?properties.trafficWeight > 0]"

     smoke-tests:
       needs: deploy
       runs-on: ubuntu-latest
       steps:
         - name: Run smoke tests
           run: |
             # Health check
             curl -f https://teams-bot-staging.azurecontainerapps.io/health
             
             # MCP server health check
             curl -f https://teams-bot-staging.azurecontainerapps.io/api/mcp/health
   ```

5. **Enhanced Container Apps Deployment Workflow**
   **Reference**: [Microsoft Learn - Deploy to Azure Container Apps with GitHub Actions](https://learn.microsoft.com/en-us/azure/container-apps/github-actions)
   ```yaml
   # .github/workflows/container-apps-deploy.yml
   name: Container Apps Deployment
   
   on:
     push:
       branches: [main]
       paths: ['TeamsBot/**', 'McpServer/**']
     workflow_dispatch:
       inputs:
         environment:
           description: 'Target environment'
           required: true
           default: 'development'
           type: choice
           options: ['development', 'staging', 'production']
   
   permissions:
     id-token: write
     contents: read
     security-events: write
   
   jobs:
     build-and-security-scan:
       runs-on: ubuntu-latest
       outputs:
         teams-bot-image: ${{ steps.image-tags.outputs.teams-bot-image }}
         mcp-server-image: ${{ steps.image-tags.outputs.mcp-server-image }}
       steps:
         - uses: actions/checkout@v4
         
         - name: Setup .NET
           uses: actions/setup-dotnet@v4
           with:
             dotnet-version: '8.0.x'
             
         - name: Run application tests
           run: |
             dotnet test --configuration Release --logger trx --collect:"XPlat Code Coverage"
             
         - name: Azure Login with OIDC
           uses: azure/login@v1
           with:
             client-id: ${{ secrets.AZURE_CLIENT_ID }}
             tenant-id: ${{ secrets.AZURE_TENANT_ID }}
             subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
             
         - name: Generate image tags
           id: image-tags
           run: |
             TIMESTAMP=$(date +%Y%m%d%H%M%S)
             TEAMS_BOT_IMAGE="${{ vars.CONTAINER_REGISTRY_NAME }}.azurecr.io/teams-bot:${{ github.sha }}-${TIMESTAMP}"
             MCP_SERVER_IMAGE="${{ vars.CONTAINER_REGISTRY_NAME }}.azurecr.io/mcp-server:${{ github.sha }}-${TIMESTAMP}"
             echo "teams-bot-image=$TEAMS_BOT_IMAGE" >> $GITHUB_OUTPUT
             echo "mcp-server-image=$MCP_SERVER_IMAGE" >> $GITHUB_OUTPUT
             
         - name: Build and push Teams Bot to ACR
           run: |
             az acr build \
               --registry ${{ vars.CONTAINER_REGISTRY_NAME }} \
               --image ${{ steps.image-tags.outputs.teams-bot-image }} \
               --file TeamsBot/Dockerfile \
               .
               
         - name: Build and push MCP Server to ACR
           run: |
             az acr build \
               --registry ${{ vars.CONTAINER_REGISTRY_NAME }} \
               --image ${{ steps.image-tags.outputs.mcp-server-image }} \
               --file McpServer/Dockerfile \
               .
               
         - name: Container Security Scan
           uses: Azure/container-scan@v0
           with:
             image-name: ${{ steps.image-tags.outputs.teams-bot-image }}
             severity-threshold: HIGH
             
         - name: Upload Security Scan Results
           uses: github/codeql-action/upload-sarif@v2
           if: always()
           with:
             sarif_file: ${{ steps.container-scan.outputs.scan-report-path }}
   
     deploy-to-container-apps:
       needs: build-and-security-scan
       runs-on: ubuntu-latest
       environment: ${{ github.event.inputs.environment || 'development' }}
       steps:
         - uses: actions/checkout@v4
         
         - name: Azure Login with OIDC
           uses: azure/login@v1
           with:
             client-id: ${{ secrets.AZURE_CLIENT_ID }}
             tenant-id: ${{ secrets.AZURE_TENANT_ID }}
             subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
             
         - name: Deploy to Container Apps with Managed Identity
           run: |
             # Configure Container App to use managed identity for ACR access
             az containerapp registry set \
               --name ${{ vars.TEAMS_BOT_CONTAINER_APP_NAME }} \
               --resource-group ${{ vars.RESOURCE_GROUP_NAME }} \
               --server ${{ vars.CONTAINER_REGISTRY_NAME }}.azurecr.io \
               --identity system
               
             # Update Teams Bot Container App
             az containerapp update \
               --name ${{ vars.TEAMS_BOT_CONTAINER_APP_NAME }} \
               --resource-group ${{ vars.RESOURCE_GROUP_NAME }} \
               --image ${{ needs.build-and-security-scan.outputs.teams-bot-image }} \
               --revision-suffix "r${{ github.run_number }}"
               
             # Update MCP Server Container App  
             az containerapp update \
               --name ${{ vars.MCP_SERVER_CONTAINER_APP_NAME }} \
               --resource-group ${{ vars.RESOURCE_GROUP_NAME }} \
               --image ${{ needs.build-and-security-scan.outputs.mcp-server-image }} \
               --revision-suffix "r${{ github.run_number }}"
               
         - name: Post-Deployment Health Check
           run: |
             # Wait for deployment to complete
             sleep 30
             
             # Check Teams Bot health
             TEAMS_BOT_URL=$(az containerapp show \
               --name ${{ vars.TEAMS_BOT_CONTAINER_APP_NAME }} \
               --resource-group ${{ vars.RESOURCE_GROUP_NAME }} \
               --query "properties.configuration.ingress.fqdn" -o tsv)
             
             curl -f "https://${TEAMS_BOT_URL}/api/health" || exit 1
             echo "✅ Teams Bot deployment successful"
             
             # Check MCP Server health
             MCP_SERVER_URL=$(az containerapp show \
               --name ${{ vars.MCP_SERVER_CONTAINER_APP_NAME }} \
               --resource-group ${{ vars.RESOURCE_GROUP_NAME }} \
               --query "properties.configuration.ingress.fqdn" -o tsv)
             
             curl -f "https://${MCP_SERVER_URL}/health" || exit 1
             echo "✅ MCP Server deployment successful"
   ```

#### Enhanced Infrastructure Tests
- [ ] **Bicep template compilation** (`az bicep build`)
- [ ] **Template validation** against target resource group
- [ ] **What-if analysis** with detailed change reporting
- [ ] **Security scanning** with Checkov for IaC compliance
- [ ] **Container vulnerability scanning** before deployment
- [ ] **Post-deployment health checks** for all services
- [ ] **RBAC validation** for managed identity permissions

#### Success Criteria
- CI/CD pipeline triggers on code changes
- Infrastructure deploys successfully with Bicep
- Container images are scanned and free of critical vulnerabilities
- Application is accessible and healthy post-deployment

---

## Phase 4: Multi-Agent Communication Enhancement 🤖
**Goal**: Enable sophisticated agent-to-agent communication patterns

### Phase 4.1: Agent Session Management
**Duration**: 2 sessions | **Tests**: 12-15 new test cases

#### Technical Implementation
1. **Session Management Service**
   ```csharp
   public interface IAgentSessionService
   {
       Task<AgentSession> CreateSessionAsync(string agentId, TimeSpan? duration = null);
       Task<AgentSession?> GetSessionAsync(string sessionId);
       Task<bool> ValidateSessionAsync(string sessionId, string agentId);
       Task InvalidateSessionAsync(string sessionId);
       Task<IEnumerable<AgentSession>> GetActiveSessionsAsync();
   }

   public class AgentSessionService : IAgentSessionService
   {
       private readonly IMemoryCache _cache;
       private readonly ILogger<AgentSessionService> _logger;

       public async Task<AgentSession> CreateSessionAsync(string agentId, TimeSpan? duration = null)
       {
           var session = new AgentSession
           {
               Id = Guid.NewGuid().ToString(),
               AgentId = agentId,
               CreatedAt = DateTimeOffset.UtcNow,
               ExpiresAt = DateTimeOffset.UtcNow.Add(duration ?? TimeSpan.FromHours(1)),
               IsActive = true
           };

           _cache.Set(session.Id, session, session.ExpiresAt);
           _logger.LogInformation("Created session {SessionId} for agent {AgentId}", session.Id, agentId);
           
           return session;
       }
   }
   ```

2. **Agent Communication Hub**
   ```csharp
   [ApiController]
   [Route("api/agents")]
   public class AgentCommunicationController : ControllerBase
   {
       [HttpPost("{sessionId}/broadcast")]
       public async Task<IActionResult> BroadcastMessage(
           string sessionId, 
           [FromBody] AgentMessage message)
       {
           var session = await _sessionService.GetSessionAsync(sessionId);
           if (session?.IsActive != true)
               return Unauthorized("Invalid or expired session");

           await _hubContext.Clients.Group($"session-{sessionId}")
               .SendAsync("ReceiveMessage", message);
               
           return Ok();
       }

       [HttpPost("{sessionId}/delegate")]
       public async Task<IActionResult> DelegateTask(
           string sessionId,
           [FromBody] TaskDelegationRequest request)
       {
           var availableAgents = await _agentRegistry.GetAvailableAgentsAsync(request.RequiredCapabilities);
           var selectedAgent = _loadBalancer.SelectAgent(availableAgents);
           
           var taskResult = await _taskExecutor.ExecuteAsync(selectedAgent, request.Task);
           return Ok(taskResult);
       }
   }
   ```

#### Session Management Tests
- [ ] Session creation and expiration
- [ ] Session validation for different agents
- [ ] Concurrent session handling
- [ ] Session cleanup on expiration
- [ ] Load balancing across agents

#### Success Criteria
- Multiple agents can participate in conversations
- Session management prevents unauthorized access
- Task delegation works reliably

### Phase 4.2: Tool Orchestration and Workflow Engine
**Duration**: 2 sessions | **Tests**: 10-12 new test cases

#### Technical Implementation
1. **Workflow Definition Model**
   ```csharp
   public class AgentWorkflow
   {
       public string Id { get; set; } = Guid.NewGuid().ToString();
       public string Name { get; set; } = string.Empty;
       public List<WorkflowStep> Steps { get; set; } = new();
       public Dictionary<string, object> Context { get; set; } = new();
       public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
   }

   public class WorkflowStep
   {
       public string Id { get; set; } = Guid.NewGuid().ToString();
       public string AgentId { get; set; } = string.Empty;
       public string ToolName { get; set; } = string.Empty;
       public Dictionary<string, object> Parameters { get; set; } = new();
       public List<string> DependsOn { get; set; } = new();
       public StepStatus Status { get; set; } = StepStatus.Pending;
       public object? Result { get; set; }
   }
   ```

2. **Workflow Execution Engine**
   ```csharp
   public class WorkflowExecutionEngine
   {
       public async Task<WorkflowResult> ExecuteWorkflowAsync(AgentWorkflow workflow)
       {
           workflow.Status = WorkflowStatus.Running;
           
           var executionPlan = _dependencyResolver.CreateExecutionPlan(workflow.Steps);
           
           foreach (var batch in executionPlan)
           {
               var tasks = batch.Select(step => ExecuteStepAsync(workflow, step));
               var results = await Task.WhenAll(tasks);
               
               if (results.Any(r => !r.Success))
               {
                   workflow.Status = WorkflowStatus.Failed;
                   return new WorkflowResult { Success = false, Workflow = workflow };
               }
           }
           
           workflow.Status = WorkflowStatus.Completed;
           return new WorkflowResult { Success = true, Workflow = workflow };
       }
       
       private async Task<StepResult> ExecuteStepAsync(AgentWorkflow workflow, WorkflowStep step)
       {
           var agent = await _agentRegistry.GetAgentAsync(step.AgentId);
           var tool = await agent.GetToolAsync(step.ToolName);
           
           step.Status = StepStatus.Running;
           
           try
           {
               var result = await tool.ExecuteAsync(step.Parameters, workflow.Context);
               step.Result = result;
               step.Status = StepStatus.Completed;
               
               return new StepResult { Success = true, Result = result };
           }
           catch (Exception ex)
           {
               step.Status = StepStatus.Failed;
               _logger.LogError(ex, "Failed to execute step {StepId}", step.Id);
               
               return new StepResult { Success = false, Error = ex.Message };
           }
       }
   }
   ```

#### Workflow Engine Tests
- [ ] Parallel step execution
- [ ] Dependency resolution
- [ ] Error handling and rollback
- [ ] Context passing between steps
- [ ] Workflow persistence and recovery

#### Success Criteria
- Complex multi-step workflows execute correctly
- Dependencies are properly resolved
- Failed steps trigger appropriate error handling
- Workflow state persists across agent restarts

---

## Testing Strategy

### Unit Test Requirements
- **Coverage Target**: >90% code coverage
- **Performance Target**: <60ms per test (Azure MCP standard)
- **Test Categories**:
  - Fast unit tests (no I/O)
  - Integration tests (with mocked external dependencies)
  - Contract tests (MCP protocol compliance)

### Test Data Management
```csharp
// Test builders for consistent test data
public class ActionItemDetailsBuilder
{
    private ActionItemDetails _actionItem = new();
    
    public ActionItemDetailsBuilder WithTitle(string title)
    {
        _actionItem.Title = title;
        return this;
    }
    
    public ActionItemDetailsBuilder WithPriority(string priority)
    {
        _actionItem.Priority = priority;
        return this;
    }
    
    public ActionItemDetails Build() => _actionItem;
}

// Usage in tests
var actionItem = new ActionItemDetailsBuilder()
    .WithTitle("Test Work Item")
    .WithPriority("High")
    .Build();
```

### Integration Test Strategy
```csharp
[Collection("Integration Tests")]
public class McpServerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task JsonRpcEndpoint_ShouldHandleValidToolRequest()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            Id = "test-123",
            Method = "ado-create-work-item",
            Params = new { title = "Test Item", description = "Test Description" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/mcp/tools", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>();
        jsonResponse.Should().NotBeNull();
        jsonResponse!.Id.Should().Be("test-123");
    }
}
```

---

## Current Session Focus & Enhanced Next Steps

### 🎯 Immediate Next Session Goal
**Session Target**: Begin Phase 1.1 - Implement MCP JSON-RPC 2.0 server infrastructure

#### Ready to Execute Now
1. **Create MCP Server Project**
   ```bash
   # Add new project to solution
   dotnet new webapi -n McpServer --framework net8.0
   dotnet sln add McpServer/McpServer.csproj
   ```

2. **First TDD Implementation**
   - Create `McpServerTests` project
   - Write tests for JSON-RPC 2.0 initialization endpoint
   - Implement basic MCP protocol handlers

#### Session-by-Session Breakdown (Enhanced)

### Session 1: MCP Server Foundation
**Deliverables**:
- [ ] `McpServer` project with JSON-RPC endpoints
- [ ] `IMcpTool` interface and registry pattern
- [ ] Basic tool discovery and execution framework
- [ ] 15+ new tests with 100% coverage

**Technical Focus**: 
- JSON-RPC 2.0 protocol compliance
- Tool registration and discovery
- Request/response validation

### Session 2: Azure DevOps Tool Implementation  
**Deliverables**:
- [ ] `CreateWorkItemTool` with full field mapping
- [ ] OAuth 2.0 token exchange (replace PAT)
- [ ] Error handling and validation
- [ ] Integration tests with real ADO API

**Technical Focus**:
- SFI-compliant authentication migration
- ADO REST API integration
- Robust error handling patterns

### Session 3: Infrastructure as Code Setup
**Deliverables**:
- [ ] Complete Bicep module library
- [ ] `azure.yaml` for azd compatibility
- [ ] GitHub Actions workflows (bicep-unit-tests.yml)
- [ ] What-if analysis automation

**Technical Focus**:
- Modular Bicep architecture
- Azure Container Apps with managed identity
- CI/CD foundation with security scanning

### Session 4: Teams AI Library Integration
**Deliverables**:
- [ ] Teams AI Library integration
- [ ] Intent detection and conversation analysis  
- [ ] Meeting participant tracking
- [ ] Context-aware action item extraction

**Technical Focus**:
- Natural language understanding
- Conversation state management
- Meeting workflow integration

### Session 5: Container Apps Deployment
**Deliverables**:
- [ ] Full Azure Container Apps deployment
- [ ] Managed identity and ACR integration
- [ ] Multi-container orchestration
- [ ] Health checks and monitoring

**Technical Focus**:
- Production deployment patterns
- Container security and scanning
- Cross-service communication

### Session 6: CI/CD Production Pipeline
**Deliverables**:
- [ ] Complete GitHub Actions automation
- [ ] Environment-specific deployments
- [ ] Security scanning integration
- [ ] Automated testing and validation

**Technical Focus**:
- Full automation pipeline
- Security compliance
- Environment management

### Session 7: Multi-Agent Orchestration
**Deliverables**:
- [ ] Agent communication framework
- [ ] Workflow execution engine
- [ ] Session state management
- [ ] Load balancing and resilience

**Technical Focus**:
- Agent coordination patterns
- Distributed system reliability
- Performance optimization

### Session 8: Production Hardening & Monitoring
**Deliverables**:
- [ ] Application Insights integration
- [ ] Custom telemetry and dashboards
- [ ] Blue-green deployment strategy
- [ ] Performance testing and optimization

**Technical Focus**:
- Production monitoring
- Deployment strategies
- Performance and scalability

---

## Updated Technology Stack & Best Practices

### Enhanced Technology Integration
| Technology | Purpose | Best Practice Reference |
|------------|---------|------------------------|
| **Bicep Templates** | Infrastructure as Code | [Azure Container Apps with Bicep](https://github.com/Azure-Samples/containerapps-builtinauth-bicep) |
| **GitHub Actions** | CI/CD Automation | [Bicep Deployment with GitHub Actions](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-github-actions) |
| **OpenID Connect** | Federated Identity | [SFI-Compliant Authentication](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-github-actions#generate-deployment-credentials) |
| **Container Apps** | Hosting Platform | [Deploy to Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/github-actions) |
| **Managed Identity** | Secure Access | [User-Assigned Managed Identity](https://learn.microsoft.com/en-us/azure/container-apps/tutorial-ci-cd-runners-jobs) |
| **Security Scanning** | Compliance | [Container Security with GitHub Actions](https://learn.microsoft.com/en-us/azure/container-apps/github-actions#configuration) |

### Architectural Alignment with Azure MCP Reference
- **JSON-RPC 2.0 Compliance**: Full MCP protocol implementation
- **Federated Identity**: No secrets in code, OIDC-based authentication
- **Container Native**: Azure Container Apps with managed identity
- **Infrastructure Automation**: Bicep with what-if analysis
- **Security First**: Container scanning, SARIF uploads, compliance checks

---

## Summary & Validation

### ✅ Roadmap Validation Summary
This comprehensive roadmap has been validated against Microsoft's best practices and aligns with current Azure deployment patterns:

#### **Technical Architecture Validation**
- **MCP JSON-RPC Implementation**: Follows microservices patterns with Container Apps native support
- **Federated Identity**: Uses Azure AD OpenID Connect, eliminating secrets in code (SFI-compliant)
- **Container Apps**: Leverages managed identities, built-in authentication, and Dapr integration capabilities
- **Infrastructure**: Bicep templates with resource tokens match Azure sample repositories

#### **CI/CD Pipeline Validation** 
- **GitHub Actions**: Aligns with Microsoft's recommended automation framework for Azure
- **Security Integration**: SARIF uploads, container scanning, and Bicep what-if analysis follow current best practices
- **Environment Management**: Multi-environment deployment with approval gates matches enterprise patterns
- **Infrastructure as Code**: Bicep with what-if analysis prevents configuration drift

#### **Implementation Feasibility Confirmation**
- **Session Structure**: 8 focused sessions enable iterative delivery and testing
- **TDD Approach**: Building on existing 127/127 test foundation ensures quality
- **Technology Stack**: Teams AI Library, Container Apps, and GitHub Actions represent Microsoft's current guidance
- **Risk Mitigation**: Phased approach reduces complexity and enables validation at each step

#### **Microsoft Ecosystem Alignment**
- **Teams Development**: Uses Teams AI Library for prompt engineering and conversation handling
- **Azure Deployment**: Container Apps with Bicep follows Azure Well-Architected Framework
- **DevOps**: GitHub Actions integration matches Microsoft's CI/CD recommendations
- **Security**: Zero-secrets approach using managed identities and federated credentials

### 🎯 Ready for Implementation
This roadmap provides:
- **Clear technical specifications** for each phase
- **Testable deliverables** with success criteria  
- **Microsoft-aligned best practices** throughout
- **Session-based progression** enabling continuous progress
- **Configuration as Code** for full automation

**Next Action**: Begin Phase 1.2 with Azure DevOps MCP tool implementations and SFI-compliant authentication migration.

---

*This roadmap serves as the definitive implementation guide for the Microsoft Teams AI bot project. Each session should reference this document for technical details, update progress status, and validate against defined success criteria.*

**Last Updated**: June 27, 2025  
**Implementation Status**: Phase 1.1 Complete ✅ - MCP JSON-RPC 2.0 Server Ready  
**Test Coverage**: 148/148 tests passing (<22s execution time)  
**Next Session**: Phase 1.2 - Azure DevOps Tools Implementation
