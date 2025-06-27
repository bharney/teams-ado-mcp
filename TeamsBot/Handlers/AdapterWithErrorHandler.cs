using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;

namespace TeamsBot.Handlers
{
    /// <summary>
    /// Bot Framework adapter with comprehensive error handling
    /// Integrates with Azure Application Insights for telemetry
    /// </summary>
    public class AdapterWithErrorHandler : CloudAdapter
    {
        public AdapterWithErrorHandler(
            BotFrameworkAuthentication auth, 
            ILogger<AdapterWithErrorHandler> logger)
            : base(auth, logger)
        {
            OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application
                logger.LogError(exception, "Exception caught in OnTurnError");

                // Send a message to the user
                var errorMessageText = "The bot encountered an error or bug.";
                var errorMessage = Activity.CreateMessageActivity();
                errorMessage.Text = errorMessageText;
                errorMessage.InputHint = InputHints.ExpectingInput;
                await turnContext.SendActivityAsync(errorMessage);

                // Send additional info in development
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    var devErrorMessage = Activity.CreateMessageActivity();
                    devErrorMessage.Text = $"Debug info: {exception.Message}";
                    await turnContext.SendActivityAsync(devErrorMessage);
                }

                // Send a trace activity for Bot Framework Emulator
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message, 
                    "https://www.botframework.com/schemas/error", "TurnError");
            };
        }
    }
}
