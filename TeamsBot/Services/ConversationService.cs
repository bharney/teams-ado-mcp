using Microsoft.Bot.Schema;
using TeamsBot.Models;

namespace TeamsBot.Services
{
    /// <summary>
    /// Service for managing conversation context and history
    /// Stores conversation data for action item extraction and analysis
    /// </summary>
    public interface IConversationService
    {
        Task StoreConversationContextAsync(IActivity activity, CancellationToken cancellationToken);
        Task<MeetingContext> GetMeetingContextAsync(string conversationId, CancellationToken cancellationToken);
        Task<IEnumerable<IActivity>> GetRecentMessagesAsync(string conversationId, int count, CancellationToken cancellationToken);
    }

    public class ConversationService : IConversationService
    {
        private readonly ILogger<ConversationService> _logger;
        // In production, this would use Azure Storage or Cosmos DB
        private static readonly Dictionary<string, List<IActivity>> _conversationHistory = new();
        private static readonly Dictionary<string, MeetingContext> _meetingContexts = new();

        public ConversationService(ILogger<ConversationService> logger)
        {
            _logger = logger;
        }

        public async Task StoreConversationContextAsync(IActivity activity, CancellationToken cancellationToken)
        {
            try
            {
                var conversationId = activity.Conversation.Id;
                
                if (!_conversationHistory.ContainsKey(conversationId))
                {
                    _conversationHistory[conversationId] = new List<IActivity>();
                }

                _conversationHistory[conversationId].Add(activity);

                // Keep only last 50 messages per conversation
                if (_conversationHistory[conversationId].Count > 50)
                {
                    _conversationHistory[conversationId].RemoveAt(0);
                }

                _logger.LogDebug($"Stored conversation activity for {conversationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing conversation context");
            }
        }

        public async Task<MeetingContext> GetMeetingContextAsync(string conversationId, CancellationToken cancellationToken)
        {
            _meetingContexts.TryGetValue(conversationId, out var context);
            return context ?? new MeetingContext { ConversationId = conversationId };
        }

        public async Task<IEnumerable<IActivity>> GetRecentMessagesAsync(string conversationId, int count, CancellationToken cancellationToken)
        {
            if (_conversationHistory.TryGetValue(conversationId, out var messages))
            {
                return messages.TakeLast(count);
            }
            return Enumerable.Empty<IActivity>();
        }
    }
}
