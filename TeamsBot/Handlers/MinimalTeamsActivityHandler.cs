using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace TeamsBot.Handlers
{
    /// <summary>
    /// Minimal Teams Activity Handler for initial testing
    /// Focuses on basic message handling and health checks
    /// </summary>
    public class MinimalTeamsActivityHandler : ActivityHandler
    {
        private readonly ILogger<MinimalTeamsActivityHandler> _logger;

        public MinimalTeamsActivityHandler(ILogger<MinimalTeamsActivityHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles message activities from Teams
        /// Simple echo bot functionality for testing
        /// </summary>
        protected override async Task OnMessageActivityAsync(
            ITurnContext<IMessageActivity> turnContext, 
            CancellationToken cancellationToken)
        {
            try
            {
                var messageText = turnContext.Activity.Text?.Trim();
                _logger.LogInformation($"Received message: {messageText}");

                if (string.IsNullOrEmpty(messageText))
                {
                    await turnContext.SendActivityAsync("I received an empty message.", cancellationToken: cancellationToken);
                    return;
                }

                // Simple command handling for testing
                if (messageText.Equals("health", StringComparison.OrdinalIgnoreCase))
                {
                    var healthMessage = $"✅ Bot is healthy! Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
                    await turnContext.SendActivityAsync(healthMessage, cancellationToken: cancellationToken);
                }
                else if (messageText.StartsWith("/test", StringComparison.OrdinalIgnoreCase))
                {
                    var testMessage = $"🧪 Test command received: {messageText}";
                    await turnContext.SendActivityAsync(testMessage, cancellationToken: cancellationToken);
                }
                else
                {
                    // Echo the message back
                    var replyText = $"Echo: {messageText}";
                    await turnContext.SendActivityAsync(replyText, cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message activity");
                await turnContext.SendActivityAsync(
                    "Sorry, I encountered an error processing your message.", 
                    cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Handles members being added to the conversation
        /// Sends welcome message
        /// </summary>
        protected override async Task OnMembersAddedAsync(
            IList<ChannelAccount> membersAdded,
            ITurnContext<IConversationUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    var welcomeMessage = @"
👋 **Welcome to the Teams ADO Bot!**

I'm a simple bot for testing. Try these commands:
• `health` - Check bot status
• `/test [message]` - Test command processing
• Any other message will be echoed back

This is a minimal implementation for testing the basic bot framework integration.
                    ";

                    var welcomeActivity = Activity.CreateMessageActivity();
                    welcomeActivity.Text = welcomeMessage;
                    
                    await turnContext.SendActivityAsync(
                        welcomeActivity,
                        cancellationToken);
                }
            }
        }
    }
}
