using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema.Teams;
using System.Text.Json;
using TeamsBot.Services;
using TeamsBot.Models;
using McpServer.Services; // Use consolidated Azure DevOps service
using McpServer.Models;   // WorkItemRequest/Result

namespace TeamsBot.Handlers
{
    /// <summary>
    /// Teams AI bot handler for meeting participation and intent detection
    /// Follows Azure MCP patterns for authentication and service architecture
    /// Enhanced with Teams AI Library for advanced conversation understanding
    /// </summary>
    public class TeamsAIActivityHandler : TeamsActivityHandler
    {
        private readonly ILogger<TeamsAIActivityHandler> _logger;
        private readonly McpServer.Services.IAzureDevOpsService _azureDevOpsService;
        private readonly IConversationIntelligenceService _conversationIntelligence;

        public TeamsAIActivityHandler(
            ILogger<TeamsAIActivityHandler> logger,
            McpServer.Services.IAzureDevOpsService azureDevOpsService,
            IConversationIntelligenceService conversationIntelligence)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _azureDevOpsService = azureDevOpsService ?? throw new ArgumentNullException(nameof(azureDevOpsService));
            _conversationIntelligence = conversationIntelligence ?? throw new ArgumentNullException(nameof(conversationIntelligence));
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Processing message: {Message}", turnContext.Activity.Text);

                // Get conversation context for AI analysis
                var context = await GetConversationContext(turnContext);

                // Use AI-powered intent detection
                var intentResult = await _conversationIntelligence.DetectIntentAsync(
                    turnContext.Activity.Text ?? string.Empty,
                    context,
                    cancellationToken);

                _logger.LogInformation("Intent detection result: {Intent} (confidence: {Confidence})",
                    intentResult.Intent, intentResult.Confidence);

                if (intentResult.IsFacilitatorPrompt && intentResult.Confidence > 0.7f)
                {
                    // Check if user has facilitator permissions
                    if (await CheckFacilitatorPermissions(turnContext, cancellationToken))
                    {
                        _logger.LogInformation("Processing facilitator prompt with high confidence");
                        await ProcessFacilitatorPrompt(turnContext, context, cancellationToken);
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(
                            MessageFactory.Text("I detected an action item request, but you don't have facilitator permissions."),
                            cancellationToken);
                    }
                }
                else if (intentResult.IsFacilitatorPrompt && intentResult.Confidence > 0.4f)
                {
                    // Medium confidence - ask for confirmation
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text($"It seems like you might want to create an action item. Could you be more specific? (Confidence: {intentResult.Confidence:P0})"),
                        cancellationToken);
                }
                else
                {
                    // Regular conversation response
                    await HandleGeneralConversation(turnContext, intentResult, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message activity");
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("I encountered an error processing your message. Please try again."),
                    cancellationToken);
            }
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var member in membersAdded)
                {
                    if (member.Id != turnContext.Activity.Recipient.Id)
                    {
                        var welcomeMessage = "Hello! I'm your AI assistant for meetings. I can help identify action items and create Azure DevOps work items when facilitators make requests.";
                        await turnContext.SendActivityAsync(MessageFactory.Text(welcomeMessage), cancellationToken);

                        _logger.LogInformation("Welcomed new member: {MemberId}", member.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnMembersAddedAsync");
            }
        }

        private async Task ProcessFacilitatorPrompt(ITurnContext<IMessageActivity> turnContext, string context, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Processing facilitator prompt: {Message}", turnContext.Activity.Text);

                // Extract action item details using AI
                var actionItem = await _conversationIntelligence.ExtractActionItemAsync(
                    turnContext.Activity.Text ?? string.Empty,
                    context,
                    cancellationToken);

                if (actionItem != null)
                {
                    // Add user information to action item
                    actionItem.CreatedBy = turnContext.Activity.From?.Name ?? "Unknown";

                    // Create Azure DevOps work item
                    var workItemId = await CreateAzureDevOpsWorkItem(actionItem, cancellationToken);

                    var responseMessage = workItemId.HasValue
                        ? $"✅ Created Azure DevOps work item #{workItemId.Value}: {actionItem.Title}"
                        : "❌ Failed to create work item. Please check the configuration.";

                    await turnContext.SendActivityAsync(MessageFactory.Text(responseMessage), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text("I couldn't extract clear action item details. Please provide more specific information about the task you'd like to create."),
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing facilitator prompt");
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Error processing the action item request."),
                    cancellationToken);
            }
        }

        private Task<bool> CheckFacilitatorPermissions(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            try
            {
                // In a real implementation, this would check:
                // 1. User's Teams role in the meeting
                // 2. Azure AD group membership
                // 3. Custom permissions stored in database

                // For now, return true for all users (implement proper role checking later)
                var userId = turnContext.Activity.From?.Id;
                _logger.LogDebug("Checking facilitator permissions for user: {UserId}", userId);

                // TODO: Implement actual permission checking using Microsoft Graph API
                // Example: Check if user is in "Meeting Facilitators" Azure AD group
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking facilitator permissions");
                return Task.FromResult(false);
            }
        }

        private Task<string> GetConversationContext(ITurnContext<IMessageActivity> turnContext)
        {
            try
            {
                // Build context from conversation metadata
                var context = new List<string>();

                // Add conversation type information
                if (turnContext.Activity.Conversation?.ConversationType != null)
                {
                    context.Add($"Conversation type: {turnContext.Activity.Conversation.ConversationType}");
                }

                // Add channel information if in Teams
                if (turnContext.Activity.ChannelData != null)
                {
                    context.Add("Teams meeting context");
                }

                // Add user information
                var userName = turnContext.Activity.From?.Name ?? "Unknown user";
                context.Add($"User: {userName}");

                // Add timestamp context
                context.Add($"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");

                return Task.FromResult(string.Join(", ", context));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error building conversation context");
                return Task.FromResult("General conversation");
            }
        }

        private async Task HandleGeneralConversation(ITurnContext<IMessageActivity> turnContext, IntentDetectionResult intentResult, CancellationToken cancellationToken)
        {
            try
            {
                var responses = new[]
                {
                    "I'm here to help with creating action items and Azure DevOps work items. Just ask me to create a task!",
                    "I can help identify action items from your conversations. Try saying 'create a task for...'",
                    "I'm listening for facilitator prompts to create work items. How can I assist you today?",
                    $"I understand you said: '{turnContext.Activity.Text}'. I'm here to help with action items and work item creation."
                };

                var response = responses[new Random().Next(responses.Length)];
                await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in general conversation handling");
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("I'm here to help with creating action items!"),
                    cancellationToken);
            }
        }

        private async Task<int?> CreateAzureDevOpsWorkItem(ActionItemDetails actionItem, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Creating Azure DevOps work item: {@ActionItem}", actionItem);
                // Map ActionItemDetails to WorkItemRequest (TeamsBot -> McpServer model)
                var request = new WorkItemRequest
                {
                    Title = actionItem.Title,
                    Description = actionItem.Description,
                    WorkItemType = actionItem.WorkItemType,
                    Priority = actionItem.Priority,
                    AssignedTo = actionItem.AssignedTo
                };

                var workItem = await _azureDevOpsService.CreateWorkItemAsync(request);

                if (workItem != null && workItem.Id > 0)
                {
                    _logger.LogInformation("Created work item {WorkItemId}", workItem.Id);
                    return workItem.Id;
                }

                _logger.LogWarning("Failed to create work item for action item: {Title}", actionItem.Title);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Azure DevOps work item");
                return null;
            }
        }
    }
}
