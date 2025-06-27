### Project Overview

**AI Agent for Microsoft Teams**: This project aims to develop an AI agent that integrates with Microsoft Teams to listen to meetings, identify facilitator prompts, and automatically create Azure DevOps work items. The agent will leverage natural language processing to understand context and extract relevant information from conversations.

### Technology Stack

#### Frontend
- **Microsoft Teams SDK**: For integrating the bot into Teams.
- **React**: For building the user interface components if needed.
- **TypeScript**: For type safety in the frontend code.

#### Backend
- **.NET 8**: Web API for handling requests and processing logic.
- **Entity Framework Core 8**: For data access and management.
- **Azure Services**: Integration with Azure Key Vault, Application Insights, and Cognitive Services for natural language processing.
- **Azure DevOps REST API**: For creating work items in ADO.
- **SignalR**: For real-time communication if needed.

#### Infrastructure & DevOps
- **Azure App Service**: For hosting the backend API.
- **Azure Container Registry**: For storing Docker images.
- **Docker**: For containerization of the application.
- **Kubernetes/Helm**: For deployment and orchestration.

### Architecture & Data Flow

1. **Teams Bot**: The bot listens to conversations in Teams meetings and processes messages.
2. **Natural Language Processing (NLP)**: Use Azure Cognitive Services to analyze conversation context and extract action items.
3. **MCP Server**: A Model Context Protocol server built in .NET that exposes the agent's capabilities, including creating ADO work items.
4. **ADO Integration**: A service that communicates with Azure DevOps REST API to create work items based on extracted information.
5. **Database**: Optional, for storing logs or conversation transcripts if needed.

### Project Structure

#### Frontend Structure (`src/app/`)
- **`/components/`**: Reusable React components for the Teams interface.
- **`/api/`**: API routes that proxy to the backend.
- **`/context/`**: Context providers for state management.
- **`/services/`**: Service layer for API calls and actions.

#### Backend Structure (`src/server/`)
- **`/Controllers/`**: API controllers for handling requests.
- **`/Services/`**: Business logic for processing conversations and interacting with ADO.
- **`/Models/`**: Domain models for work items and conversation context.
- **`/Extensions/`**: Extension methods for configuration and setup.

### Implementation Steps

1. **Set Up the Development Environment**
   - Install .NET 8 SDK, Docker, and Azure CLI.
   - Create a new repository for the project.

2. **Create the Teams Bot**
   - Register the bot in the Azure Bot Framework.
   - Implement the bot using the Microsoft Bot Framework SDK.
   - Set up the bot to listen for messages in Teams meetings.

3. **Implement Natural Language Processing**
   - Integrate Azure Cognitive Services for NLP.
   - Create a service to analyze conversation transcripts and extract action items.

4. **Develop the MCP Server**
   - Implement the MCP server in .NET to expose tools for creating ADO work items.
   - Define tools for processing requests from the Teams bot.

5. **Integrate with Azure DevOps**
   - Create a service that interacts with the Azure DevOps REST API to create work items.
   - Handle authentication using Azure Managed Identity.

6. **Set Up Database (Optional)**
   - If needed, set up a database using Entity Framework Core to store logs or conversation transcripts.

7. **Containerization and Deployment**
   - Create Dockerfiles for the backend and bot.
   - Set up Helm charts for Kubernetes deployment.
   - Deploy the application to Azure App Service or Azure Kubernetes Service.

8. **Testing and Validation**
   - Implement unit tests for the bot and backend services.
   - Test the integration with Teams and Azure DevOps.

9. **Documentation**
   - Document the architecture, setup instructions, and usage guidelines in a README file.

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

#### MCP Server Tool Definition
```csharp
public class AdoTool : IMcpTool
{
    [ToolCommand("create-work-item")]
    public async Task<WorkItemResult> CreateItem([ToolParameter("description")] string description)
    {
        var workItem = await _adoClient.CreateWorkItemAsync(new { title = description });
        return new WorkItemResult(workItem.Id, workItem.Url);
    }
}
```

### Conclusion

This project will create an AI agent that enhances productivity in Microsoft Teams by automating the creation of Azure DevOps work items based on meeting conversations. By following the architecture and patterns established in the Azure MCP project, we ensure a robust and scalable solution. As the project progresses, we will refine the implementation details and expand the functionality based on user feedback and requirements.