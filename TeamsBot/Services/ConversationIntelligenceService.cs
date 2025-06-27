using Microsoft.Extensions.Logging;
using System.Text.Json;
using TeamsBot.Configuration;
using TeamsBot.Models;

namespace TeamsBot.Services
{
    /// <summary>
    /// Advanced conversation intelligence service for intent detection and action item extraction
    /// Implements Azure MCP patterns with fallback to keyword-based detection
    /// Future enhancement: Integrate with Azure OpenAI or OpenAI API for AI-powered analysis
    /// </summary>
    public interface IConversationIntelligenceService
    {
        Task<IntentDetectionResult> DetectIntentAsync(string message, string context, CancellationToken cancellationToken = default);
        Task<ActionItemDetails?> ExtractActionItemAsync(string message, string context, CancellationToken cancellationToken = default);
        Task<bool> IsFacilitatorPromptAsync(string message, string context, CancellationToken cancellationToken = default);
    }

    public class ConversationIntelligenceService : IConversationIntelligenceService
    {
        private readonly ILogger<ConversationIntelligenceService> _logger;
        private readonly ISecureConfigurationProvider _configProvider;

        public ConversationIntelligenceService(
            ILogger<ConversationIntelligenceService> logger,
            ISecureConfigurationProvider configProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        }

        public async Task<IntentDetectionResult> DetectIntentAsync(string message, string context, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Detecting intent for message: {Message}", message);

                var config = await _configProvider.GetConfigurationAsync();
                
                // For now, use keyword-based detection
                // TODO: Integrate with Azure OpenAI or OpenAI API when configuration is available
                if (!string.IsNullOrEmpty(config.TeamsAi.OpenAiApiKey) && config.TeamsAi.EnableIntentDetection)
                {
                    // TODO: Implement AI-based intent detection
                    _logger.LogInformation("AI-based intent detection would be used here (OpenAI key configured)");
                }

                return await FallbackIntentDetection(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in intent detection, falling back to keyword-based detection");
                return await FallbackIntentDetection(message);
            }
        }

        public async Task<ActionItemDetails?> ExtractActionItemAsync(string message, string context, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Extracting action item from message: {Message}", message);

                var config = await _configProvider.GetConfigurationAsync();

                // For now, use keyword-based extraction
                // TODO: Integrate with Azure OpenAI or OpenAI API when configuration is available
                if (!string.IsNullOrEmpty(config.TeamsAi.OpenAiApiKey) && config.TeamsAi.EnableActionItemExtraction)
                {
                    // TODO: Implement AI-based action item extraction
                    _logger.LogInformation("AI-based action item extraction would be used here (OpenAI key configured)");
                }

                return FallbackActionItemExtraction(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in action item extraction, falling back to keyword-based extraction");
                return FallbackActionItemExtraction(message);
            }
        }

        public async Task<bool> IsFacilitatorPromptAsync(string message, string context, CancellationToken cancellationToken = default)
        {
            try
            {
                var intentResult = await DetectIntentAsync(message, context, cancellationToken);
                return intentResult.IsFacilitatorPrompt && intentResult.Confidence > 0.7f;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking facilitator prompt");
                return false;
            }
        }

        private Task<IntentDetectionResult> FallbackIntentDetection(string message)
        {
            var lowMessage = message.ToLowerInvariant();
            var facilitatorKeywords = new[]
            {
                "create work item", "create task", "add to backlog", "action item", "action items",
                "follow up", "follow-up", "todo", "to do", "task for", "assign to", 
                "create bug", "create story", "create epic", "file a bug", "log a task",
                "new task", "new work item", "add task", "track this", "make a note"
            };

            var urgencyKeywords = new[] { "urgent", "asap", "high priority", "critical", "immediately" };
            var lowPriorityKeywords = new[] { "low priority", "when possible", "eventually", "nice to have" };

            bool containsKeywords = facilitatorKeywords.Any(keyword => lowMessage.Contains(keyword));
            bool hasUrgency = urgencyKeywords.Any(keyword => lowMessage.Contains(keyword));
            bool isLowPriority = lowPriorityKeywords.Any(keyword => lowMessage.Contains(keyword));

            float confidence = 0.2f;
            if (containsKeywords)
            {
                confidence = 0.8f;
                if (hasUrgency) confidence += 0.1f;
                if (message.Contains("@") || lowMessage.Contains("assign")) confidence += 0.05f;
            }

            var intent = containsKeywords ? "create_work_item" : "general_conversation";
            
            return Task.FromResult(new IntentDetectionResult
            {
                IsFacilitatorPrompt = containsKeywords,
                Confidence = Math.Min(confidence, 1.0f),
                Intent = intent,
                Reasoning = containsKeywords 
                    ? $"Contains facilitator keywords. Confidence boosted by: {(hasUrgency ? "urgency, " : "")}{(message.Contains("@") ? "assignment, " : "")}".TrimEnd(' ', ',')
                    : "No facilitator keywords detected"
            });
        }

        private ActionItemDetails FallbackActionItemExtraction(string message)
        {
            return new ActionItemDetails
            {
                Title = ExtractTitle(message),
                Description = message,
                Priority = ExtractPriority(message),
                AssignedTo = ExtractAssignee(message),
                WorkItemType = ExtractWorkItemType(message),
                EstimatedEffort = ExtractEffort(message),
                DueDate = ExtractDueDate(message)
            };
        }

        private string ExtractTitle(string message)
        {
            // Remove common prefixes
            var cleanMessage = message;
            var prefixes = new[] { "create task", "create work item", "add task", "file a bug", "create bug", "action item" };
            
            foreach (var prefix in prefixes)
            {
                if (cleanMessage.ToLowerInvariant().StartsWith(prefix))
                {
                    cleanMessage = cleanMessage.Substring(prefix.Length).Trim(' ', ':', '-');
                    break;
                }
            }

            var words = cleanMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var title = words.Length > 10 ? string.Join(' ', words.Take(10)) : cleanMessage;
            return title.Length > 100 ? title.Substring(0, 97) + "..." : title;
        }

        private string ExtractPriority(string message)
        {
            var lowMessage = message.ToLowerInvariant();
            if (lowMessage.Contains("urgent") || lowMessage.Contains("high priority") || lowMessage.Contains("asap") || lowMessage.Contains("critical"))
                return "High";
            if (lowMessage.Contains("low priority") || lowMessage.Contains("when possible") || lowMessage.Contains("nice to have"))
                return "Low";
            return "Medium";
        }

        private string? ExtractAssignee(string message)
        {
            var patterns = new[] { "assign to ", "for ", "task for ", "@" };
            foreach (var pattern in patterns)
            {
                var index = message.ToLowerInvariant().IndexOf(pattern);
                if (index >= 0)
                {
                    var afterPattern = message.Substring(index + pattern.Length);
                    var words = afterPattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0)
                        return words[0].Trim(',', '.', '!', '?', '@');
                }
            }
            return null;
        }

        private string ExtractWorkItemType(string message)
        {
            var lowMessage = message.ToLowerInvariant();
            if (lowMessage.Contains("bug") || lowMessage.Contains("defect") || lowMessage.Contains("issue") || lowMessage.Contains("error"))
                return "Bug";
            if (lowMessage.Contains("epic"))
                return "Epic";
            if (lowMessage.Contains("story") || lowMessage.Contains("user story") || lowMessage.Contains("feature") || lowMessage.Contains("requirement"))
                return "User Story";
            return "Task";
        }

        private string? ExtractEffort(string message)
        {
            var patterns = new[] { "estimate ", "points ", "hours ", "days ", "effort " };
            foreach (var pattern in patterns)
            {
                var index = message.ToLowerInvariant().IndexOf(pattern);
                if (index >= 0)
                {
                    var afterPattern = message.Substring(index + pattern.Length);
                    var words = afterPattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0 && words[0].Any(char.IsDigit))
                        return words[0].Trim(',', '.', '!', '?');
                }
            }
            return null;
        }

        private string? ExtractDueDate(string message)
        {
            var patterns = new[] { "due ", "by ", "deadline ", "complete by " };
            foreach (var pattern in patterns)
            {
                var index = message.ToLowerInvariant().IndexOf(pattern);
                if (index >= 0)
                {
                    var afterPattern = message.Substring(index + pattern.Length);
                    var words = afterPattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0)
                    {
                        // Simple date extraction - could be enhanced with proper date parsing
                        var dateCandidate = string.Join(' ', words.Take(3));
                        return dateCandidate.Trim(',', '.', '!', '?');
                    }
                }
            }
            return null;
        }
    }
}
