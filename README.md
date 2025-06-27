### Project Overview

**AI Agent for Microsoft Teams**: This project aims to develop an AI agent that integrates with Microsoft Teams to listen to meetings, identify action items based on facilitator prompts, and automatically create Azure DevOps work items. The agent will leverage natural language processing to understand context and extract relevant information from conversations.

### Technology Stack

#### Frontend
- **Microsoft Teams SDK**: For integrating the bot into Teams.
- **React**: For building any UI components if needed.
- **TypeScript**: For type safety and better development experience.

#### Backend
- **.NET 8**: Web API for handling requests and business logic.
- **Entity Framework Core 8**: For data access and management.
- **Azure Functions**: For serverless processing of events and commands.
- **Azure Cognitive Services**: For natural language processing and understanding.
- **Azure DevOps REST API**: For creating work items in ADO.
- **ASP.NET Core Identity**: For user authentication and management.

#### Infrastructure & DevOps
- **Azure App Service**: For hosting the backend API.
- **Azure SQL Database**: For storing application data.
- **Azure Redis Cache**: For caching frequently accessed data.
- **Docker**: For containerization of the application.
- **Azure Kubernetes Service (AKS)**: For orchestrating containers if needed.

### Architecture & Data Flow

1. **Teams Bot**: The bot listens to meeting conversations and processes messages.
2. **Natural Language Processing (NLP)**: The bot uses Azure Cognitive Services to analyze conversation context and identify action items.
3. **MCP Server**: A Model Context Protocol server built in .NET that exposes the agent's capabilities (e.g., creating ADO items).
4. **ADO Integration**: The bot communicates with Azure DevOps REST API to create work items based on extracted information.
5. **Database**: Store user preferences, logs, and any other necessary data.

### Project Structure

#### Frontend Structure
- **`/components/`**: Reusable React components for the Teams UI.
- **`/services/`**: Service layer for API calls to the backend.
- **`/context/`**: Context providers for managing state.

#### Backend Structure
- **`/Controllers/`**: API controllers for handling requests.
- **`/Services/`**: Business logic for processing commands and interacting with ADO.
- **`/Models/`**: Data models for the application.
- **`/Extensions/`**: Extension methods for configuration and setup.

### Implementation Steps

1. **Set Up the Development Environment**:
   - Install .NET 8 SDK, Azure CLI, and Docker.
   - Configure User Secrets for local development (see `docs/USER_SECRETS_SETUP.md`).
   - Set up Azure resources using azd deployment.

2. **Create the Teams Bot**:
   - Use the Microsoft Bot Framework to create a bot that can join meetings and listen for messages.
   - Implement message handling to trigger actions based on specific commands (e.g., "/create work item").

3. **Integrate Azure Cognitive Services**:
   - Use Azure Cognitive Services for speech-to-text conversion and natural language understanding.
   - Implement logic to extract action items and relevant details from the conversation.

4. **Implement the MCP Server**:
   - Create a .NET-based MCP server that exposes tools for creating ADO work items.
   - Define tools and their parameters for the MCP server.

5. **Integrate with Azure DevOps**:
   - Implement a service that communicates with the Azure DevOps REST API to create work items based on the extracted information.
   - Handle authentication using Azure Active Directory and managed identities.

6. **Testing and Validation**:
   - Write unit tests for the bot and backend services.
   - Perform integration testing to ensure the bot correctly creates ADO work items based on conversation context.

7. **Deployment**:
   - Containerize the application using Docker.
   - Deploy the application to Azure App Service or AKS.
   - Set up CI/CD pipelines for automated deployments.

### Local Development Setup

This project uses **Visual Studio User Secrets** for secure local development configuration instead of `.env` files.

#### Quick Start with DevSetup CLI:
```bash
# Check your development environment status
.\devsetup.bat status

# Get help creating Azure DevOps PAT
.\devsetup.bat login --organization your-ado-org

# Configure your PAT after creating it
.\devsetup.bat pat --token YOUR_PAT_TOKEN --organization your-ado-org
```

#### Manual Setup:
1. **Clone the repository**
2. **Configure secrets for both projects:**
   ```bash
   # TeamsBot secrets
   cd TeamsBot
   dotnet user-secrets set "AzureDevOps:PersonalAccessToken" "your-ado-pat"
   dotnet user-secrets set "MicrosoftAppId" "your-bot-app-id"
   
   # McpServer secrets  
   cd ../McpServer
   dotnet user-secrets set "AzureDevOps:PersonalAccessToken" "your-ado-pat"
   ```

3. **See complete setup guide:** `docs/USER_SECRETS_SETUP.md`

#### Benefits:
- 🔐 **Secure**: Secrets encrypted and stored outside repository
- 🎯 **Integrated**: Seamless .NET configuration system integration  
- 🚫 **Safe**: No risk of accidentally committing secrets
- 👥 **Isolated**: Per-user and per-machine secret isolation
- 🛠️ **CLI Helper**: DevSetup CLI automates the authentication process

#### CLI Documentation:
See `DevSetupCli/README.md` for complete CLI usage guide.

#### Migration from .env:
If upgrading from a previous version, see `docs/USER_SECRETS_MIGRATION.md` for migration instructions.

### Example Code Snippets

#### Teams Bot Trigger
```csharp
// In the Teams bot, handle the message activity
if (context.Activity.Text.Contains("/create work item", StringComparison.OrdinalIgnoreCase))
{
    var actionItem = ExtractActionItem(context.Activity.Text);
    var workItem = await _adoService.CreateWorkItemAsync(actionItem);
    await context.SendActivityAsync($"Created ADO item: {workItem.Id}");
}
```

#### MCP Tool Definition
```csharp
public class AdoTool : IMcpTool
{
    [ToolCommand("create-work-item")]
    public async Task<WorkItemResult> CreateItem([ToolParameter("description")] string description)
    {
        var workItem = await _adoClient.CreateWorkItemAsync(new { description });
        return new WorkItemResult(workItem.Id, workItem.Url);
    }
}
```

### Conclusion

This project will create a robust AI agent that enhances productivity in Microsoft Teams by automating the creation of Azure DevOps work items based on meeting conversations. By following the architecture and patterns established in the Azure MCP project, we ensure a scalable and maintainable solution. As the project evolves, we will continue to refine the implementation and expand its capabilities.