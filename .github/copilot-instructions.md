# GitHub Copilot Instructions - Teams AI Bot with Azure DevOps Integration

## 🎯 Project Mission & Identity
You are assisting with an **enterprise-grade Microsoft Teams AI bot** that integrates with Azure DevOps using **Model Context Protocol (MCP)** patterns. The bot listens to Teams meetings, identifies facilitator prompts, and automatically creates Azure DevOps work items using federated identity and AI-driven conversation analysis.

### 📊 Current Project State
```
STATUS: Ready to Begin Implementation (Phase 1.1)
TESTS: 127/127 passing with <60ms performance
ARCHITECTURE: Teams Bot (echo) + ADO Service (PAT) + MCP Client (skeleton)
NEXT: MCP Server JSON-RPC 2.0 implementation
```

### 🏗️ Target Architecture
```ascii
[Teams Meeting] ➜ [Teams AI Bot] ➜ [MCP Server] ➜ [Azure DevOps API]
                        ↓              ↓              ↓
                 [Intent Detection] [JSON-RPC] [Work Items]
                        ↓              ↓              ↓
              [Azure Container Apps] ➜ [Managed Identity] ➜ [SFI Compliant]
```

### 🧬 Technical DNA
- **Language**: C# (.NET 8)
- **Testing**: xUnit + Moq + FluentAssertions (TDD mandatory)
- **Cloud**: Azure (Container Apps, Key Vault, Managed Identity)
- **Protocol**: JSON-RPC 2.0 (MCP standard)
- **Security**: SFI-compliant (no secrets in code)
- **CI/CD**: GitHub Actions + Bicep IaC + OIDC

---

## 🚀 Implementation Roadmap Reference

**ALWAYS** reference `PROJECT_ROADMAP.md` for implementation guidance. Current phases:

| Phase | Focus Area | Status | Duration |
|-------|------------|---------|----------|
| **1.1** | **MCP Server JSON-RPC** | **🎯 CURRENT** | **1-2 sessions** |
| 1.2 | Azure DevOps Tools | Next | 1 session |
| 1.3 | Federated Identity | Planned | 2 sessions |
| 2.1 | Bicep Infrastructure | Planned | 2-3 sessions |
| 3.1 | GitHub Actions CI/CD | Planned | 2-3 sessions |

**Session-Based Development**: Each session has specific deliverables, test targets, and success criteria defined in the roadmap.

---

## 🧪 Test-Driven Development (TDD) Protocol

### Mandatory TDD Process
```markdown
🔴 RED: Write failing test first
🟢 GREEN: Minimal implementation to pass
♻️ REFACTOR: Improve code quality
✅ VERIFY: All 127+ tests still pass in <60ms
```

### Testing Standards
- **Framework**: xUnit + Moq + FluentAssertions
- **Coverage**: >90% code coverage required
- **Performance**: <60ms per test execution
- **Pattern**: Test builders for consistent test data
- **Integration**: Real Azure DevOps API tests with mocked credentials

### Test Examples
```csharp
// ✅ GOOD: Descriptive test with FluentAssertions
[Fact]
public async Task CreateWorkItemTool_ShouldCreateWorkItem_WhenValidParametersProvided()
{
    // Arrange
    var tool = new CreateWorkItemTool(_mockAdoService.Object);
    var parameters = new McpToolParameters()
        .Add("title", "Test Work Item")
        .Add("description", "Test Description");

    // Act
    var result = await tool.ExecuteAsync(parameters);

    // Assert
    result.Success.Should().BeTrue();
    result.Data.Should().NotBeNull();
    _mockAdoService.Verify(x => x.CreateWorkItemAsync(It.IsAny<WorkItemRequest>()), Times.Once);
}
```

---

## 🔒 Azure Security & Best Practices

### Security Requirements (SFI-Compliant)
- **❌ NO SECRETS in code**: Never store PATs, connection strings, or credentials
- **✅ USE Managed Identity**: DefaultAzureCredential for all Azure services
- **✅ USE Key Vault**: Azure Key Vault for configuration management
- **✅ USE OIDC**: OpenID Connect for GitHub Actions authentication

### Azure Identity Pattern
```csharp
// ✅ CORRECT: Use managed identity
services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
services.AddScoped<IAzureDevOpsService, FederatedIdentityAdoService>();

// ❌ INCORRECT: Never use PAT tokens in production
// config.AzureDevOps.PersonalAccessToken = "pat_token"; // ❌ FORBIDDEN
```

### Infrastructure as Code (Bicep)
- **Template Pattern**: Use resource tokens for unique naming
- **Module Structure**: Separate modules for each Azure service
- **Parameter Files**: Environment-specific `.bicepparam` files
- **Validation**: Always run `az bicep build` and `az deployment group validate`

### Container Apps Deployment
```bicep
// ✅ CORRECT: Managed identity with ACR access
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    configuration: {
      registries: [{
        server: '${containerRegistry.name}.azurecr.io'
        identity: managedIdentity.id  // ✅ No passwords needed
      }]
    }
  }
}
```

---

## 🏛️ Architecture Patterns & Standards

### MCP Server Implementation
```csharp
// JSON-RPC 2.0 endpoint pattern
[ApiController]
[Route("api/mcp")]
public class McpController : ControllerBase
{
    [HttpPost("tools")]
    public async Task<IActionResult> ExecuteTool([FromBody] JsonRpcRequest request)
    {
        // Validate JSON-RPC 2.0 compliance
        // Execute tool via registry pattern
        // Return structured response
    }
}
```

### Tool Registry Pattern
```csharp
// Dynamic tool discovery
public interface IMcpTool
{
    string Name { get; }
    Task<McpToolResult> ExecuteAsync(McpToolParameters parameters);
}

// Tools auto-register via dependency injection
services.AddScoped<IMcpTool, CreateWorkItemTool>();
services.AddScoped<IMcpTool, GetWorkItemTool>();
```

### Teams AI Library Integration
```csharp
// Use Teams AI Library for intent detection
services.AddTeamsAI(options => {
    options.AI.Planner = new ActionPlanner();
    options.AI.Model = "gpt-4o";  // Latest model for best results
});
```

---

## 🔄 CI/CD & Deployment Patterns

### GitHub Actions Best Practices
- **OIDC Authentication**: No secrets, use federated identity
- **Bicep Validation**: Run `az bicep build` and what-if analysis
- **Security Scanning**: Container scanning with SARIF uploads
- **Environment Gates**: Manual approval for production deployments

### Workflow Structure
```yaml
# ✅ CORRECT: OpenID Connect authentication
- name: Azure Login
  uses: azure/login@v1
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
    # No client-secret needed with federated credentials

# ✅ CORRECT: Bicep what-if analysis
- name: Run What-If Analysis
  run: |
    az deployment group what-if \
      --resource-group ${{ vars.RESOURCE_GROUP_NAME }} \
      --template-file infra/main.bicep \
      --parameters @infra/parameters/dev.bicepparam
```

### Azure Developer CLI (azd) Integration
```yaml
# azure.yaml pattern for azd compatibility
services:
  teams-bot:
    project: ./TeamsBot
    language: dotnet
    host: containerapp
  mcp-server:
    project: ./McpServer
    language: dotnet
    host: containerapp
```

---

## 💬 Context-Driven Prompt Engineering

### 🎯 ALWAYS Include This Context
When requesting help, **automatically inject** this context into every prompt:

```markdown
<!-- AUTOMATIC CONTEXT INJECTION -->
Project: Teams AI Bot + Azure DevOps MCP Integration
Phase: [Current Phase from PROJECT_ROADMAP.md]
Architecture: Teams Bot → MCP Server → Azure DevOps API
Security: SFI-compliant, DefaultAzureCredential, no PAT tokens
Testing: TDD with xUnit/Moq/FluentAssertions (127/127 tests passing)
Performance: <60ms test execution, <500ms API response
Target: [Specific deliverable for this session]
<!-- END CONTEXT -->
```

### 🚀 Effective Prompt Patterns

#### For New Features (TDD Approach)
```
PROMPT TEMPLATE:
"@azure I need to implement [FEATURE] following TDD for [PROJECT CONTEXT].

Context:
- Phase: [X.Y from roadmap]
- Architecture: [Teams Bot → MCP Server → Azure DevOps]
- Security: Use DefaultAzureCredential, no secrets in code
- Testing: Write failing tests first with xUnit/Moq/FluentAssertions

Requirements:
- [Specific requirement 1]
- [Specific requirement 2]
- Maintain 127/127 test suite passing
- Follow JSON-RPC 2.0 for MCP endpoints

Generate tests first, then minimal implementation."
```

#### For Azure Infrastructure
```
PROMPT TEMPLATE:
"@azure Generate Bicep templates for [AZURE SERVICE] with these requirements:

Context:
- Project: Teams AI Bot with Azure DevOps MCP integration
- Target: Azure Container Apps with managed identity
- Security: SFI-compliant (no secrets, use OIDC, Key Vault)

Requirements:
- User-assigned managed identity for ACR access
- Resource token naming pattern: 'ca-teams-bot-${resourceToken}'
- Parameter files for dev/staging/prod environments
- Integration with existing infrastructure

Follow Azure best practices for Container Apps and include what-if validation."
```

#### For Debugging/Troubleshooting
```
PROMPT TEMPLATE:
"@azure Help debug [ISSUE] in the Teams AI Bot project.

Context:
- Architecture: Teams Bot → MCP Server → Azure DevOps API
- Current state: [Brief description of what's happening]
- Expected: [What should happen]
- Error: [Specific error message or behavior]

Project details:
- .NET 8 with xUnit testing (127/127 tests currently passing)
- Azure Container Apps deployment
- JSON-RPC 2.0 MCP protocol
- Managed identity authentication

Please provide step-by-step troubleshooting approach."
```

#### For Code Review/Refactoring
```
PROMPT TEMPLATE:
"@azure Review this [CODE TYPE] for the Teams AI Bot project:

Context:
- Component: [TeamsBot/McpServer/Tests]
- Purpose: [What this code does]
- Standards: SOLID principles, TDD, Azure best practices
- Security: SFI-compliant (no secrets in code)

[CODE BLOCK]

Please check for:
- Security compliance (no hardcoded secrets)
- Test coverage and TDD patterns
- Azure best practices (managed identity usage)
- Performance considerations (<60ms tests, <500ms API)
- Alignment with PROJECT_ROADMAP.md patterns"
```

### 🏷️ Smart Context Tags
Use these tags to automatically inject relevant context:

- `#current-phase` → Current roadmap phase and deliverables
- `#architecture` → Full system architecture diagram
- `#security-reqs` → SFI compliance and Azure security patterns
- `#test-standards` → TDD requirements and test patterns
- `#azure-patterns` → Container Apps, Bicep, managed identity patterns
- `#mcp-protocol` → JSON-RPC 2.0 and tool registry patterns

### 📋 Context Validation Checklist
Before submitting any prompt, ensure you've included:

- [ ] **Project Identity**: Teams AI Bot + Azure DevOps MCP integration
- [ ] **Current Phase**: Reference to PROJECT_ROADMAP.md phase
- [ ] **Architecture Context**: Which component you're working on
- [ ] **Security Requirements**: SFI-compliant, managed identity
- [ ] **Testing Approach**: TDD with specific framework requirements
- [ ] **Performance Targets**: Test and API response time expectations
- [ ] **Specific Deliverable**: What you want to accomplish this session

### 🎯 Example Enhanced Prompts

#### ✅ EXCELLENT Prompt
```
@azure Implement MCP JSON-RPC 2.0 endpoint for Azure DevOps work item creation.

Context:
- Project: Teams AI Bot with Azure DevOps MCP integration (Phase 1.1)
- Component: New McpServer project in .NET 8
- Architecture: Teams Bot → McpServer (JSON-RPC) → Azure DevOps API
- Security: Use DefaultAzureCredential, no PAT tokens in production
- Testing: TDD with xUnit/Moq/FluentAssertions (maintain 127/127 passing)

Requirements:
1. Create McpController with [HttpPost] tools endpoint
2. Implement JsonRpcRequest/Response models
3. Add CreateWorkItemTool with proper parameter validation
4. Write 15+ unit tests before implementation
5. Ensure <60ms test execution performance
6. Follow tool registry pattern for extensibility

Success Criteria:
- JSON-RPC 2.0 compliant protocol handling
- Integration with existing AzureDevOpsService
- All new tests passing with FluentAssertions
- Zero regression in existing 127 test suite
```

#### ❌ POOR Prompt
```
Create an endpoint for work items
```

#### ✅ GOOD Prompt (Infrastructure)
```
@azure Generate Container Apps Bicep template with managed identity for Teams bot.

Context:
- Project: Teams AI Bot deployment to Azure Container Apps
- Architecture: Multi-container (TeamsBot + McpServer) with ACR
- Security: SFI-compliant using user-assigned managed identity
- Environment: Support dev/staging/prod parameter files

Requirements:
1. Container Apps Environment with Log Analytics
2. User-assigned managed identity with AcrPull role
3. Resource token naming: 'ca-teams-bot-${resourceToken}'
4. CORS policy configuration for bot endpoints
5. Environment variables from Key Vault references

Follow Azure best practices and include what-if validation steps.
```

#### ❌ POOR Prompt
```
Make a bicep file for containers
```

---

## 📊 Session-Based Context Management

### 🔄 Auto-Context for Development Sessions
Every development session should begin with this context check:

```markdown
## Session Context Check
- [ ] Current Phase: [Check PROJECT_ROADMAP.md for active phase]
- [ ] Previous Session: [What was completed last session]
- [ ] Test Status: [Run 'dotnet test' to verify 127/127 passing]
- [ ] Session Goal: [Specific deliverable from roadmap]
- [ ] Time Estimate: [Expected session duration]
- [ ] Dependencies: [What needs to be ready before starting]
```

### 🎯 Context Templates by Activity Type

#### 🧪 Testing & TDD Sessions
```
Context for TDD Implementation:
- Test Framework: xUnit + Moq + FluentAssertions
- Current Coverage: 127/127 tests passing in <60ms
- TDD Cycle: Red (failing test) → Green (minimal impl) → Refactor
- Target: Add [X] new tests for [Feature]
- Pattern: Test builders for consistent data setup
```

#### 🏗️ Infrastructure Sessions  
```
Context for Infrastructure Work:
- IaC Tool: Bicep templates with parameter files
- Deployment: Azure Container Apps with managed identity
- Security: SFI-compliant (no secrets, OIDC, Key Vault)
- Validation: 'az bicep build' + 'az deployment group what-if'
- Target: [Specific Azure resource or deployment pipeline]
```

#### 🔧 Feature Implementation Sessions
```
Context for Feature Development:
- Component: [TeamsBot/McpServer/Shared]
- Architecture Layer: [API/Service/Model/Handler]
- Dependencies: [External services, Azure resources]
- Integration Points: [Teams SDK, Azure DevOps API, MCP protocol]
- Target: [Specific functionality to implement]
```

### 📋 Project State Tracking

#### Current Technical Debt
```
Known Technical Debt (update after each session):
- [ ] Migration from PAT to federated identity (Phase 1.3)
- [ ] Bicep infrastructure implementation (Phase 2.1)
- [ ] CI/CD pipeline automation (Phase 3.1)
- [ ] Production logging and monitoring
- [ ] Load testing and performance optimization
```

#### Recent Decisions & Context
```
Latest Architectural Decisions:
1. [Date] - Decision about [Topic]
2. [Date] - Chose [Technology] for [Reason]
3. [Date] - Implemented [Pattern] because [Justification]
```

---

## 🚨 Common Pitfalls & Solutions

### Security Anti-Patterns
```csharp
// ❌ NEVER: Store secrets in configuration
public class BadConfiguration
{
    public string AzureDevOpsToken { get; set; } = "pat_12345"; // ❌ FORBIDDEN
}

// ✅ CORRECT: Use managed identity
public class SecureAdoService
{
    private readonly TokenCredential _credential;
    public SecureAdoService(TokenCredential credential) => _credential = credential;
}
```

### Testing Anti-Patterns
```csharp
// ❌ BAD: No assertions
[Fact]
public void TestSomething() 
{
    var result = _service.DoSomething();
    // Missing assertions
}

// ✅ GOOD: Clear arrange-act-assert
[Fact]
public void DoSomething_ShouldReturnExpectedResult_WhenValidInput()
{
    // Arrange
    var input = "test";
    
    // Act
    var result = _service.DoSomething(input);
    
    // Assert
    result.Should().NotBeNull();
    result.Value.Should().Be("expected");
}
```

### Infrastructure Anti-Patterns
```bicep
// ❌ BAD: Hardcoded names
resource badContainerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'my-app' // ❌ Not unique
}

// ✅ GOOD: Resource token pattern
var resourceToken = toLower(uniqueString(subscription().id, environmentName))
resource goodContainerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'ca-teams-bot-${resourceToken}' // ✅ Unique and descriptive
}
```

---

## 📚 Reference Documentation

### Key Resources
- **Project Roadmap**: `PROJECT_ROADMAP.md` - Implementation phases and technical details
- **Azure Container Apps**: [Microsoft Learn - Deploy to Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/github-actions)
- **Bicep Best Practices**: [Azure Sample - Container Apps with Bicep](https://github.com/Azure-Samples/containerapps-builtinauth-bicep)
- **GitHub Actions**: [Deploy Bicep with GitHub Actions](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-github-actions)
- **Teams AI Library**: [Build AI Agents in Teams](https://learn.microsoft.com/en-us/microsoftteams/platform/toolkit/build-an-ai-agent-in-teams)

### Quick Reference Commands
```bash
# Test execution
dotnet test --configuration Release --logger trx

# Bicep validation
az bicep build --file infra/main.bicep
az deployment group validate --template-file infra/main.bicep

# Container build
az acr build --registry myregistry --image teams-bot:latest .

# Azure Developer CLI
azd init
azd up
azd deploy
```

---

## 🎯 Current Session Focus

**IMMEDIATE GOAL**: Implement MCP Server JSON-RPC 2.0 endpoints (Phase 1.1)

### Ready to Execute
1. Create `McpServer` project: `dotnet new webapi -n McpServer`
2. Add JSON-RPC protocol handlers with TDD approach
3. Implement tool registry and discovery pattern
4. Ensure 15+ new tests with 100% coverage

**Next Session**: Azure DevOps tool implementation with federated identity migration

---

*This document serves as the definitive guide for all development activities. Reference it for every implementation decision, architectural choice, and coding standard. Update progress in PROJECT_ROADMAP.md after each session.*

**Last Updated**: June 27, 2025  
**Implementation Status**: Phase 1.1 Ready to Begin  
**Test Coverage**: 127/127 tests passing