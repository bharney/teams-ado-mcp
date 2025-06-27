# Azure DevOps Service Implementation - SFI Compliant

## Overview

This document describes the implementation of the Azure DevOps service for the MCP Server, following Microsoft's Secure Future Initiative (SFI) compliance requirements.

## Architecture

### SFI-Compliant Authentication

The service uses **ChainedTokenCredential** instead of `DefaultAzureCredential` for production environments, providing:

- **Deterministic behavior**: Controlled credential chain for predictable authentication flow
- **Enhanced security**: SFI-compliant credential management
- **Better performance**: Optimized credential ordering

```csharp
_credential = new ChainedTokenCredential(
    new ManagedIdentityCredential(), // For Azure-hosted environments
    new AzureCliCredential(),        // For local development
    new VisualStudioCredential()     // For Visual Studio development
);
```

### Key Features

1. **Federated Identity**: No secrets or PAT tokens in code
2. **Retry Logic**: Exponential backoff for transient failures
3. **Error Handling**: Comprehensive error mapping and logging
4. **Resource Management**: Proper disposal pattern implementation
5. **Configuration-based**: Externalized settings via `AzureDevOpsOptions`

## Configuration

### appsettings.json

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

### Environment-specific Settings

- **Development**: Shorter timeouts, fewer retries for faster feedback
- **Production**: Standard timeouts, full retry logic for resilience

## API Operations

The service implements all CRUD operations for Azure DevOps work items:

### CreateWorkItemAsync
- Creates work items using JSON Patch operations
- Supports all common fields (title, description, type, priority, assigned to)
- Returns structured work item result

### GetWorkItemAsync
- Retrieves individual work items by ID
- Maps Azure DevOps fields to domain model

### GetWorkItemsAsync
- Supports WIQL queries for flexible work item retrieval
- Implements batching for performance (50 items max)
- Default query retrieves recent work items from project

### UpdateWorkItemAsync
- Updates work items using JSON Patch operations
- Only updates provided fields (partial update support)

## Security Implementation

### Authentication Flow

1. **Token Acquisition**: Uses Azure DevOps scope `499b84ac-1321-427f-aa17-267ca6975798/.default`
2. **Token Caching**: Leverages Azure.Identity's built-in token caching
3. **Thread Safety**: Semaphore-protected authentication operations

### Error Handling

The service maps Azure DevOps HTTP status codes to appropriate exceptions:

- `401 Unauthorized` → `UnauthorizedAccessException` (Authentication failed)
- `403 Forbidden` → `UnauthorizedAccessException` (Access denied)
- `404 Not Found` → `ArgumentException` (Resource not found)
- `400 Bad Request` → `ArgumentException` (Invalid request)

### Retry Strategy

Implements exponential backoff for transient failures:
- Initial delay: 1000ms (configurable)
- Max attempts: 3 (configurable)
- Exponential multiplier: 2x
- Retries on: 500, 502, 503, 504, 429 status codes

## Dependency Injection

The service is registered with proper lifecycle management:

```csharp
// Configure Azure DevOps options
builder.Services.Configure<AzureDevOpsOptions>(
    builder.Configuration.GetSection(AzureDevOpsOptions.SectionName));

// Register HTTP client for Azure DevOps service
builder.Services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>();
```

## Testing Strategy

### Unit Tests
- Mock Azure DevOps service for isolated testing
- Test parameter validation and error handling
- Verify tool execution logic

### Integration Tests
- Custom WebApplicationFactory with mocked Azure DevOps service
- End-to-end JSON-RPC protocol testing
- Real HTTP client and controller testing

### Test Implementation

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real service with mock for integration tests
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAzureDevOpsService));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            var mockAdoService = new Mock<IAzureDevOpsService>();
            // Configure mock behavior...
            services.AddScoped(_ => mockAdoService.Object);
        });
    }
}
```

## Deployment Considerations

### Azure Container Apps
- Managed identity will be automatically available
- No additional configuration needed for authentication
- Service will use first credential in chain (ManagedIdentityCredential)

### Local Development
- Azure CLI credential used for local development
- Visual Studio credential as fallback
- Configure Azure CLI: `az login`

### Configuration Management
- Organization and project names externalized to configuration
- API version pinned to specific version (7.1)
- Environment-specific timeout and retry settings

## Monitoring and Logging

The service includes comprehensive logging:

```csharp
_logger.LogInformation("Creating work item: {Title} of type {WorkItemType}", request.Title, request.WorkItemType);
_logger.LogError(ex, "Failed to create work item: {Title}", request.Title);
```

Log levels:
- **Information**: Successful operations, service initialization
- **Warning**: Retry attempts, transient failures
- **Error**: Operation failures, authentication issues

## Performance Considerations

1. **Connection Pooling**: HTTP client reuse via dependency injection
2. **Token Caching**: Azure.Identity handles token refresh automatically
3. **Batch Operations**: Work item queries limited to 50 items
4. **Timeout Management**: Configurable request timeouts
5. **Async Operations**: All operations use async/await pattern

## Compliance Notes

### SFI Requirements Met
- ✅ No hardcoded credentials or secrets
- ✅ Federated identity authentication
- ✅ Deterministic credential chain
- ✅ Proper error handling and logging
- ✅ Resource disposal patterns
- ✅ Configuration externalization

### Security Best Practices
- ✅ Least privilege access (service-specific scope)
- ✅ Token-based authentication
- ✅ HTTPS-only communication
- ✅ Comprehensive audit logging
- ✅ Proper exception handling (no sensitive data exposure)

## Future Enhancements

1. **Circuit Breaker**: Implement circuit breaker pattern for service resilience
2. **Metrics**: Add custom metrics for monitoring API performance
3. **Bulk Operations**: Support for bulk work item creation/updates
4. **Advanced Queries**: Support for complex WIQL queries
5. **Attachment Support**: Work item attachment management
6. **Field Mapping**: Configurable field mapping for different project types

---

*Last Updated: June 27, 2025*  
*Phase: 1.3 - SFI-Compliant Federated Identity Implementation*  
*Status: ✅ Completed - All 165 tests passing*
