// Main Bicep template for Teams-Azure DevOps MCP integration
// This template creates the complete infrastructure for a production deployment
// using Azure Container Apps with supporting services

targetScope = 'resourceGroup'

// Parameters
param environmentName string = 'teams-ado-mcp'
param location string = resourceGroup().location
param containerAppName string = 'teams-ado-mcp-app'
param mcpServerAppName string = 'mcp-server'

// Resource naming using a consistent token for uniqueness
param resourceToken string = toLower(uniqueString(subscription().id, resourceGroup().id, environmentName))

// Container image configuration
param mcpServerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest' // Base image for initial deployment
param teamsAppImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest' // Base image for initial deployment

// Configuration parameters
param logRetentionInDays int = 30
param enableApplicationInsights bool = true

// Variables for consistent naming
var resourceNames = {
  containerAppsEnvironment: 'cae-${resourceToken}'
  containerRegistry: 'teamsacr${resourceToken}' // Ensure minimum 5 characters with prefix
  keyVault: 'kv-${resourceToken}'
  logAnalyticsWorkspace: 'log-${resourceToken}'
  applicationInsights: 'appi-${resourceToken}'
  userAssignedIdentity: 'id-${resourceToken}'
  mcpServerApp: '${mcpServerAppName}-${resourceToken}'
  teamsApp: '${containerAppName}-${resourceToken}'
}

// Tags for all resources
var commonTags = {
  'azd-env-name': environmentName
  'azd-service-name': containerAppName
  project: 'teams-ado-mcp'
  environment: 'production'
}

// User-assigned managed identity for SFI-compliant authentication
resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: resourceNames.userAssignedIdentity
  location: location
  tags: commonTags
}

// Container Registry for storing container images
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: resourceNames.containerRegistry
  location: location
  tags: commonTags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false // Use managed identity instead of admin credentials
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: 'Disabled'
    networkRuleBypassOptions: 'AzureServices'
  }
}

// Role assignment: Grant AcrPull permission to the managed identity for the container registry
resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, userAssignedIdentity.id, '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') // AcrPull role
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    description: 'Grant AcrPull access to managed identity for container app'
  }
}

// Log Analytics Workspace for centralized logging
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: resourceNames.logAnalyticsWorkspace
  location: location
  tags: commonTags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: logRetentionInDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
      disableLocalAuth: false // Allow service principal access
    }
    workspaceCapping: {
      dailyQuotaGb: 1 // Limit daily ingestion to 1GB
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Application Insights for application monitoring and telemetry
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = if (enableApplicationInsights) {
  name: resourceNames.applicationInsights
  location: location
  tags: commonTags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    DisableLocalAuth: false // Allow instrumentation key authentication
  }
}

// Key Vault for secure secret storage
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: resourceNames.keyVault
  location: location
  tags: commonTags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForTemplateDeployment: true
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: false
    enableRbacAuthorization: true // Use RBAC instead of access policies
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Role assignment: Grant Key Vault Secrets User permission to the managed identity
resource keyVaultSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, userAssignedIdentity.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User role
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    description: 'Grant Key Vault Secrets User access to managed identity'
  }
}

// Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: resourceNames.containerAppsEnvironment
  location: location
  tags: commonTags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false // Single zone for cost optimization
  }
}

// MCP Server Container App
resource mcpServerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: resourceNames.mcpServerApp
  location: location
  tags: commonTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 5000
        transport: 'http'
        corsPolicy: {
          allowedOrigins: ['*']
          allowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
          allowedHeaders: ['*']
          allowCredentials: false
        }
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: userAssignedIdentity.id
        }
      ]
      secrets: [
        {
          name: 'appinsights-connection-string'
          value: enableApplicationInsights ? applicationInsights.properties.ConnectionString : ''
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'mcp-server'
          image: mcpServerImage
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:5000'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'appinsights-connection-string'
            }
            {
              name: 'KeyVault__VaultUri'
              value: keyVault.properties.vaultUri
            }
            {
              name: 'AzureDevOps__UseKeyVault'
              value: 'true'
            }
            {
              name: 'AzureDevOps__KeyVaultSecretName'
              value: 'azure-devops-pat'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 5000
                scheme: 'HTTP'
              }
              initialDelaySeconds: 30
              periodSeconds: 30
              timeoutSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 5000
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    acrPullRoleAssignment
    keyVaultSecretsUserRoleAssignment
  ]
}

// Teams Bot Container App (placeholder for future Teams Bot implementation)
resource teamsApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: resourceNames.teamsApp
  location: location
  tags: commonTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 3978
        transport: 'http'
        corsPolicy: {
          allowedOrigins: ['*']
          allowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
          allowedHeaders: ['*']
          allowCredentials: false
        }
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: userAssignedIdentity.id
        }
      ]
      secrets: [
        {
          name: 'appinsights-connection-string'
          value: enableApplicationInsights ? applicationInsights.properties.ConnectionString : ''
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'teams-bot'
          image: teamsAppImage
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:3978'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'appinsights-connection-string'
            }
            {
              name: 'KeyVault__VaultUri'
              value: keyVault.properties.vaultUri
            }
            {
              name: 'McpServer__BaseUrl'
              value: 'https://${mcpServerApp.properties.configuration.ingress.fqdn}'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 3978
                scheme: 'HTTP'
              }
              initialDelaySeconds: 30
              periodSeconds: 30
              timeoutSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 3978
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 5
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    acrPullRoleAssignment
    keyVaultSecretsUserRoleAssignment
  ]
}

// Outputs for use by other scripts and deployment tools
output containerAppsEnvironmentName string = containerAppsEnvironment.name
output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
output applicationInsightsName string = enableApplicationInsights ? applicationInsights.name : ''
output applicationInsightsConnectionString string = enableApplicationInsights ? applicationInsights.properties.ConnectionString : ''
output userAssignedIdentityName string = userAssignedIdentity.name
output userAssignedIdentityId string = userAssignedIdentity.id
output userAssignedIdentityClientId string = userAssignedIdentity.properties.clientId
output mcpServerAppName string = mcpServerApp.name
output mcpServerAppFqdn string = mcpServerApp.properties.configuration.ingress.fqdn
output teamsAppName string = teamsApp.name
output teamsAppFqdn string = teamsApp.properties.configuration.ingress.fqdn
output resourceGroupName string = resourceGroup().name
output subscriptionId string = subscription().subscriptionId
output RESOURCE_GROUP_ID string = resourceGroup().id
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = 'https://${containerRegistry.properties.loginServer}'
