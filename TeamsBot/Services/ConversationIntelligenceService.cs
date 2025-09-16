using Microsoft.Extensions.Logging;
using System.Text.Json;
using TeamsBot.Configuration;
using TeamsBot.Models;
using Azure.AI.OpenAI; // Azure OpenAI integration
using OpenAI.Chat; // Chat types

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
    private readonly IAzureOpenAIClient? _openAI; // optional, falls back if null

        public ConversationIntelligenceService(
            ILogger<ConversationIntelligenceService> logger,
            ISecureConfigurationProvider configProvider,
            IAzureOpenAIClient? openAI = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            _openAI = openAI; // may be null when not configured
        }

        public async Task<IntentDetectionResult> DetectIntentAsync(string message, string context, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Detecting intent for message: {Message}", message);

                var config = await _configProvider.GetConfigurationAsync();
                if (_openAI != null && config.TeamsAi.EnableIntentDetection)
                {
                    var aiResult = await InvokeIntentModel(message, context, cancellationToken);
                    if (aiResult != null)
                        return aiResult;
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
                if (_openAI != null && config.TeamsAi.EnableActionItemExtraction)
                {
                    var aiItem = await InvokeExtractionModel(message, context, cancellationToken);
                    if (aiItem != null)
                        return aiItem;
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

        private async Task<IntentDetectionResult?> InvokeIntentModel(string message, string context, CancellationToken ct)
        {
            try
            {
                var system = "You classify if a Teams chat message is a facilitator prompt to create an Azure DevOps work item. Return strict JSON with fields: isFacilitatorPrompt (bool), intent (string), confidence (0-1 float), reasoning (string). If not about work items, intent is general_conversation.";
                var user = $"Message: {message}\nContext: {context}";
                var chat = new ChatMessage[]
                {
                    ChatMessage.CreateSystemMessage(system),
                    ChatMessage.CreateUserMessage(user)
                };
                var completion = await _openAI!.CompleteChatAsync(chat, ct);
                var content = completion.Content.FirstOrDefault()?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(content)) return null;
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                return new IntentDetectionResult
                {
                    IsFacilitatorPrompt = root.GetProperty("isFacilitatorPrompt").GetBoolean(),
                    Intent = root.GetProperty("intent").GetString() ?? "general_conversation",
                    Confidence = (float)root.GetProperty("confidence").GetDouble(),
                    Reasoning = root.GetProperty("reasoning").GetString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure OpenAI intent model failed, using fallback");
                return null;
            }
        }

        private async Task<ActionItemDetails?> InvokeExtractionModel(string message, string context, CancellationToken ct)
        {
            try
            {
                var system = "Extract structured action item details from a Teams message if it is a facilitator prompt. Return JSON with: title, description, priority (High|Medium|Low), assignedTo (nullable), workItemType (Task|Bug|User Story|Epic), estimatedEffort (nullable), dueDate (nullable). Keep title concise.";
                var user = $"Message: {message}\nContext: {context}";
                var chat = new ChatMessage[]
                {
                    ChatMessage.CreateSystemMessage(system),
                    ChatMessage.CreateUserMessage(user)
                };
                var completion = await _openAI!.CompleteChatAsync(chat, ct);
                var content = completion.Content.FirstOrDefault()?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(content)) return null;
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                return new ActionItemDetails
                {
                    Title = root.GetProperty("title").GetString() ?? FallbackActionItemExtraction(message).Title,
                    Description = root.GetProperty("description").GetString() ?? message,
                    Priority = root.TryGetProperty("priority", out var p) ? p.GetString() ?? "Medium" : "Medium",
                    AssignedTo = root.TryGetProperty("assignedTo", out var a) ? a.GetString() : null,
                    WorkItemType = root.TryGetProperty("workItemType", out var w) ? w.GetString() ?? "Task" : "Task",
                    EstimatedEffort = root.TryGetProperty("estimatedEffort", out var e) ? e.GetString() : null,
                    DueDate = root.TryGetProperty("dueDate", out var d) ? d.GetString() : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure OpenAI extraction model failed, using fallback");
                return null;
            }
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
