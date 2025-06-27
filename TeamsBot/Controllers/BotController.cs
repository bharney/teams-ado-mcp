using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;

namespace TeamsBot.Controllers
{
    /// <summary>
    /// Bot Controller handles incoming HTTP POST requests from Microsoft Teams
    /// Routes activities to the bot logic via the Bot Framework adapter
    /// Integrates with Azure MCP for ADO work item creation
    /// </summary>
    [ApiController]
    [Route("api/messages")]
    public class BotController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly IBot _bot;
        private readonly ILogger<BotController> _logger;

        public BotController(
            IBotFrameworkHttpAdapter adapter, 
            IBot bot,
            ILogger<BotController> logger)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Primary endpoint for Teams bot messages
        /// Processes incoming activities and delegates to bot logic
        /// </summary>
        [HttpPost]
        public async Task PostAsync()
        {
            try
            {
                _logger.LogInformation("Processing bot message from Teams");
                
                // Delegate the processing of the HTTP POST to the adapter
                // The adapter will invoke the bot's logic
                await _adapter.ProcessAsync(Request, Response, _bot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bot message");
                throw;
            }
        }

        /// <summary>
        /// Health check endpoint for bot availability
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }
}
